/**
 * availability.js
 * -----------------------------------------------------------------------
 * This file handles two things:
 *   1. getAvailableSlots — figuring out which time slots are free
 *   2. createBooking — actually creating a booking
 *
 * Why this lives on the server (not the app):
 *   1. The app never has a fixed list of times. Every call asks the
 *      server "what's free right now?" and gets a fresh answer.
 *   2. "Now" means the SERVER's clock (Google's time servers), not the
 *      phone's clock. The phone's time could be wrong or faked, so we
 *      never trust it for deciding if a slot is bookable.
 *   3. luxon (a date library) handles all the time math — no manual
 *      string work, no time zone bugs.
 *
 * This file reads from these Firestore collections:
 *   - services
 *   - businessHours
 *   - blockedTime
 *   - bookings
 *
 * Note: getAvailableSlots and createBooking share the same real logic
 * (computeAvailableSlots). We don't have one Cloud Function call another —
 * that's not a supported pattern. Both just call the shared function.
 * -----------------------------------------------------------------------
 */

const { onCall, HttpsError } = require("firebase-functions/v2/https");
const admin = require("firebase-admin");
const { DateTime } = require("luxon");

// Initialize Firebase if not already done.
if (admin.apps.length === 0) {
  admin.initializeApp();
}
const db = admin.firestore();

// The business operates in one timezone. Change it here, once.
const BUSINESS_TIMEZONE = "Africa/Johannesburg";

// How often a new slot could start (in minutes).
// Example: every 15 minutes means slots can start at 9:00, 9:15, 9:30...
// (The service's real duration still decides how long each one is.)
const SLOT_STEP_MINUTES = 15;

// Minimum notice before a booking (no booking something happening right now).
const MIN_NOTICE_MINUTES = 60;

/**
 * getAvailableSlots
 * Input:  { serviceIds: string[], date: "YYYY-MM-DD" }
 *         serviceIds[0] is the main service, the rest are add-ons.
 * Output: { slots: [{ startTime, endTime, startIso, endIso }] }
 *
 * Called by the app to find out what's free.
 */
exports.getAvailableSlots = onCall(async (request) => {
  const { serviceIds, date } = request.data || {};
  validateSlotsInput(serviceIds, date);
  return computeAvailableSlots(serviceIds, date);
});

/**
 * createBooking
 * Input:  { serviceIds, date, startTime, notes? }
 * Output: { bookingId }
 *
 * Called by the app to actually book a slot.
 *
 * IMPORTANT: This re-checks the slot before writing anything. A slot
 * shown as free 5 minutes ago could be taken by now. The server is the
 * real gatekeeper — not the UI.
 */
exports.createBooking = onCall(async (request) => {
  const { serviceIds, date, startTime, notes } = request.data || {};
  const uid = request.auth && request.auth.uid;

  if (!uid) {
    throw new HttpsError("unauthenticated", "Must be signed in to book.");
  }
  validateSlotsInput(serviceIds, date);
  if (!startTime) {
    throw new HttpsError("invalid-argument", "startTime is required.");
  }

  // Re-check the slot is still free.
  const availability = await computeAvailableSlots(serviceIds, date);
  const stillFree = availability.slots.some((s) => s.startTime === startTime);

  if (!stillFree) {
    throw new HttpsError("failed-precondition", "That slot is no longer available — please pick another time.");
  }

  // Re-fetch and re-total everything on the server.
  // The price shown on the Review Booking screen is never trusted here.
  const { durationMinutes, services } = await fetchServicesAggregate(serviceIds);
  const totalAmount = services.reduce((sum, s) => sum + (s.price || 0), 0);
  const depositAmount = services.reduce((sum, s) => sum + (s.depositAmount || 0), 0);

  // Work out the end time.
  const targetDate = DateTime.fromISO(date, { zone: BUSINESS_TIMEZONE }).set(parseHms(startTime));
  const endTime = targetDate.plus({ minutes: durationMinutes });

  // Save the booking to Firestore.
  const bookingRef = await db.collection("bookings").add({
    clientId: uid,
    serviceId: serviceIds[0], // Main service (kept simple for queries)
    services: services.map((s) => ({
      serviceId: s.id,
      name: s.name,
      price: s.price || 0,
      depositAmount: s.depositAmount || 0,
      durationMinutes: s.durationMinutes,
      isAddOn: s.id !== serviceIds[0],
    })),
    date,
    startTime,
    endTime: endTime.toFormat("HH:mm"),
    status: "pending",
    depositAmount,
    totalAmount,
    notes: notes || "",
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  });

  return { bookingId: bookingRef.id };
});

// ---------------------------------------------------------------------
// Shared logic
// ---------------------------------------------------------------------

// Checks the input for getAvailableSlots and createBooking.
function validateSlotsInput(serviceIds, date) {
  if (!Array.isArray(serviceIds) || serviceIds.length === 0 || serviceIds.some((id) => typeof id !== "string")) {
    throw new HttpsError("invalid-argument", "serviceIds must be a non-empty array of strings.");
  }
  if (!date || !/^\d{4}-\d{2}-\d{2}$/.test(date)) {
    throw new HttpsError("invalid-argument", "date must be an ISO string, e.g. 2026-10-15.");
  }
}

/**
 * The real slot-computation logic.
 * Both getAvailableSlots and createBooking call this directly.
 * It is NOT a Cloud Function — just a plain async function.
 */
async function computeAvailableSlots(serviceIds, date) {
  const targetDate = DateTime.fromISO(date, { zone: BUSINESS_TIMEZONE });
  if (!targetDate.isValid) {
    throw new HttpsError("invalid-argument", "date could not be parsed.");
  }

  // 1. Load all the services and get total duration + buffer.
  //    Duration is SUMMED (add-ons extend the same session).
  //    Buffer is the LARGEST (it only needs to happen once).
  const { durationMinutes, bufferMinutes } = await fetchServicesAggregate(serviceIds);

  // 2. Load business hours for that weekday (0 = Sunday, 1 = Monday, etc.)
  const weekday = targetDate.weekday % 7; // luxon: 1=Mon..7=Sun -> 0=Sun..6=Sat
  const hoursSnap = await db.collection("businessHours").doc(String(weekday)).get();
  const hours = hoursSnap.exists ? hoursSnap.data() : null;

  // If the salon is closed that day, no slots are available.
  if (!hours || hours.isClosed || !hours.openTime || !hours.closeTime) {
    return { slots: [] };
  }

  const openAt = targetDate.set(parseHms(hours.openTime));
  const closeAt = targetDate.set(parseHms(hours.closeTime));

  // 3. Load blocked periods (holidays, staff leave, etc.) that overlap this date.
  //    Only ONE filter here ("start <") because Firestore doesn't support
  //    range filters on two fields. The other half of the check happens
  //    in memory below.
  const dayStartUtc = targetDate.startOf("day").toUTC().toJSDate();
  const dayEndUtc = targetDate.endOf("day").toUTC().toJSDate();
  const blockedSnap = await db.collection("blockedTime").where("start", "<", dayEndUtc).get();
  const blockedRanges = blockedSnap.docs
    .map((d) => toRange(d.data(), BUSINESS_TIMEZONE))
    .filter((r) => r.end.toJSDate() > dayStartUtc);

  // 4. Load existing bookings for that day (not cancelled).
  const bookingsSnap = await db
    .collection("bookings")
    .where("date", "==", date)
    .where("status", "in", ["pending", "confirmed"])
    .get();
  const bookedRanges = bookingsSnap.docs.map((d) => toRange(d.data(), BUSINESS_TIMEZONE));

  // 5. Get "now" from the server clock. This is what enforces the
  //    minimum notice — never anything sent by the client.
  const now = DateTime.now().setZone(BUSINESS_TIMEZONE);
  const earliestBookable = now.plus({ minutes: MIN_NOTICE_MINUTES });

  // 6. Walk through the day in steps and keep any slot that fits.
  const slots = [];
  let cursor = openAt;
  while (cursor.plus({ minutes: durationMinutes }) <= closeAt) {
    const slotStart = cursor;
    const slotEnd = cursor.plus({ minutes: durationMinutes });

    // Add the buffer before and after the slot.
    const bufferedStart = slotStart.minus({ minutes: bufferMinutes });
    const bufferedEnd = slotEnd.plus({ minutes: bufferMinutes });

    // Check three things:
    const inThePast = slotStart < earliestBookable;
    const overlapsBlocked = blockedRanges.some((r) => rangesOverlap(bufferedStart, bufferedEnd, r.start, r.end));
    const overlapsBooked = bookedRanges.some((r) => rangesOverlap(bufferedStart, bufferedEnd, r.start, r.end));

    // Only keep the slot if it passes all three checks.
    if (!inThePast && !overlapsBlocked && !overlapsBooked) {
      slots.push({
        startTime: slotStart.toFormat("HH:mm"),
        endTime: slotEnd.toFormat("HH:mm"),
        startIso: slotStart.toISO(),
        endIso: slotEnd.toISO(),
      });
    }

    // Move to the next candidate start time.
    cursor = cursor.plus({ minutes: SLOT_STEP_MINUTES });
  }

  return { slots };
}

/**
 * Fetches every service in serviceIds and works out:
 *   - Total duration (sum of all services — add-ons extend the same session)
 *   - Buffer (the largest single buffer — it only happens once)
 */
async function fetchServicesAggregate(serviceIds) {
  // Load every service in parallel.
  const snaps = await Promise.all(serviceIds.map((id) => db.collection("services").doc(id).get()));

  // If any service doesn't exist, throw an error.
  const missing = snaps.filter((s) => !s.exists);
  if (missing.length > 0) {
    throw new HttpsError("not-found", "One or more selected services were not found.");
  }

  const services = snaps.map((s) => ({ id: s.id, ...s.data() }));

  // If any service is inactive, throw an error.
  const inactive = services.filter((s) => s.isActive === false);
  if (inactive.length > 0) {
    throw new HttpsError("failed-precondition", `"${inactive[0].name}" is no longer available.`);
  }

  // Sum the durations, take the largest buffer.
  const durationMinutes = services.reduce((sum, s) => sum + (s.durationMinutes || 0), 0);
  const bufferMinutes = Math.max(0, ...services.map((s) => s.bufferMinutes || 0));

  if (!durationMinutes || durationMinutes <= 0) {
    throw new HttpsError("failed-precondition", "Selected services have no valid duration configured.");
  }

  return { durationMinutes, bufferMinutes, services };
}

// Turns "09:30" into { hour: 9, minute: 30, second: 0, millisecond: 0 }.
function parseHms(hhmm) {
  const [hour, minute] = hhmm.split(":").map(Number);
  return { hour, minute, second: 0, millisecond: 0 };
}

// Converts a Firestore document into a start/end DateTime range.
function toRange(doc, zone) {
  const start = doc.start.toDate ? DateTime.fromJSDate(doc.start.toDate(), { zone }) : DateTime.fromISO(doc.startTime, { zone });
  const end = doc.end.toDate ? DateTime.fromJSDate(doc.end.toDate(), { zone }) : DateTime.fromISO(doc.endTime, { zone });
  return { start, end };
}

// Checks if two time ranges overlap.
function rangesOverlap(aStart, aEnd, bStart, bEnd) {
  return aStart < bEnd && bStart < aEnd;
}