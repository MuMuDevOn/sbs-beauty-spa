/**
 * This file handles two things:
 *   1. getAvailableSlots — figuring out which time slots are free
 *   2. createBooking — actually creating a booking
 * This file reads from these Firestore collections:
 *   - services
 *   - businessHours
 *   - blockedTime
 *   - bookings
 */

const { onCall, HttpsError } = require("./secure");
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

// A client can only reschedule if the appointment is at least this many hours away.
const RESCHEDULE_NOTICE_HOURS = 24;

/**
 * getAvailableSlots
 * Input:  { serviceIds: string[], date: "YYYY-MM-DD" }
 *         serviceIds[0] is the main service, the rest are add-ons.
 * Output: { slots: [{ startTime, endTime, startIso, endIso }] }
 *
 * Called by the app to find out what's free.
 */
exports.getAvailableSlots = onCall(async (request) => {
  const { serviceIds, date, excludeBookingId } = request.data || {};
  validateSlotsInput(serviceIds, date);

  // Rescheduling: the booking being moved must not block its own new slot,
  // and its services come from the booking itself (not from the client).
  if (excludeBookingId) {
    const booking = await loadOwnBooking(request, excludeBookingId);
    return computeAvailableSlots(bookingServiceIds(booking), date, excludeBookingId);
  }

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

  // The check and the save happen together under a per-date lock, so two clients
  // tapping the same slot at the same moment can never both get it.
  const bookingId = await db.runTransaction(async (tx) => {
    await lockDate(tx, date);

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
    const bookingRef = db.collection("bookings").doc();
    tx.create(bookingRef, {
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
    return bookingRef.id;
  });

  return { bookingId };
});

/**
 * rescheduleBooking
 * Input:  { bookingId, date: "YYYY-MM-DD", startTime: "HH:mm" }
 * Output: { bookingId, date, startTime, endTime }
 *
 * Moves a client's own booking to a new free slot.
 * Rules (all enforced here, never trusted from the app):
 *   - the booking must belong to the caller and still be pending/confirmed
 *   - the CURRENT appointment must be at least 24 hours away
 *   - the new slot must still be free
 */
exports.rescheduleBooking = onCall(async (request) => {
  const { bookingId, date, startTime } = request.data || {};

  if (!date || !/^\d{4}-\d{2}-\d{2}$/.test(date)) {
    throw new HttpsError("invalid-argument", "date must be an ISO string, e.g. 2026-10-15.");
  }
  if (!startTime || !/^\d{2}:\d{2}$/.test(startTime)) {
    throw new HttpsError("invalid-argument", "startTime must look like 09:30.");
  }

  const booking = await loadOwnBooking(request, bookingId);

  if (!["pending", "confirmed"].includes(booking.status)) {
    throw new HttpsError("failed-precondition", "This booking can no longer be rescheduled.");
  }

  // 24-hour rule, measured against the CURRENT appointment time, using the server clock.
  const currentStart = DateTime.fromISO(booking.date, { zone: BUSINESS_TIMEZONE }).set(parseHms(booking.startTime));
  const now = DateTime.now().setZone(BUSINESS_TIMEZONE);
  if (currentStart.diff(now, "hours").hours < RESCHEDULE_NOTICE_HOURS) {
    throw new HttpsError(
      "failed-precondition",
      `Bookings can only be rescheduled at least ${RESCHEDULE_NOTICE_HOURS} hours before the appointment. Please contact the salon.`
    );
  }

  // The check and the move happen together under the new date's lock, so nobody else
  // can take the slot in between. (This booking's own current slot doesn't count against it.)
  const serviceIds = bookingServiceIds(booking);
  const endTime = await db.runTransaction(async (tx) => {
    await lockDate(tx, date);

    const availability = await computeAvailableSlots(serviceIds, date, bookingId);
    if (!availability.slots.some((s) => s.startTime === startTime)) {
      throw new HttpsError("failed-precondition", "That slot is no longer available - please pick another time.");
    }

    const { durationMinutes } = await fetchServicesAggregate(serviceIds);
    const newStart = DateTime.fromISO(date, { zone: BUSINESS_TIMEZONE }).set(parseHms(startTime));
    const newEnd = newStart.plus({ minutes: durationMinutes }).toFormat("HH:mm");

    tx.update(db.collection("bookings").doc(bookingId), {
      date,
      startTime,
      endTime: newEnd,
      previousDate: booking.date,
      previousStartTime: booking.startTime,
      rescheduledAt: admin.firestore.FieldValue.serverTimestamp(),
      rescheduleCount: admin.firestore.FieldValue.increment(1),
    });
    return newEnd;
  });

  return { bookingId, date, startTime, endTime };
});

// ---------------------------------------------------------------------
// Shared logic
// ---------------------------------------------------------------------

// Per-date lock used to prevent double booking.
// Every booking/reschedule for a date reads and then writes the same small document.
// If two requests for the same date overlap, Firestore makes the second one retry,
// and on the retry it sees the first booking and the slot is correctly refused.
// MUST be called before any write in the transaction.
async function lockDate(tx, date) {
  const ref = db.collection("bookingLocks").doc(date);
  await tx.get(ref);
  tx.set(ref, { updatedAt: admin.firestore.FieldValue.serverTimestamp() });
}

// Loads a booking and makes sure it belongs to the signed-in caller.
async function loadOwnBooking(request, bookingId) {
  const uid = request.auth && request.auth.uid;
  if (!uid) {
    throw new HttpsError("unauthenticated", "Must be signed in.");
  }
  if (!bookingId || typeof bookingId !== "string") {
    throw new HttpsError("invalid-argument", "bookingId is required.");
  }
  const snap = await db.collection("bookings").doc(bookingId).get();
  if (!snap.exists) {
    throw new HttpsError("not-found", "Booking not found.");
  }
  const booking = snap.data();
  if (booking.clientId !== uid) {
    throw new HttpsError("permission-denied", "This booking doesn't belong to you.");
  }
  return booking;
}

// The services a booking was made with (main service first).
function bookingServiceIds(booking) {
  const ids = Array.isArray(booking.services) ? booking.services.map((s) => s.serviceId).filter(Boolean) : [];
  return ids.length > 0 ? ids : [booking.serviceId];
}

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
async function computeAvailableSlots(serviceIds, date, excludeBookingId = null) {
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
  const bookedRanges = bookingsSnap.docs
    .filter((d) => d.id !== excludeBookingId) // when rescheduling, a booking never blocks itself
    .map((d) => bookingRange(d.data(), BUSINESS_TIMEZONE));

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

// A booking stores a date ("2026-10-20") plus start/end times ("08:00" / "08:30"),
// not Timestamps, so its range is built from those.
function bookingRange(booking, zone) {
  const day = DateTime.fromISO(booking.date, { zone });
  return {
    start: day.set(parseHms(booking.startTime)),
    end: day.set(parseHms(booking.endTime)),
  };
}

// Checks if two time ranges overlap.
function rangesOverlap(aStart, aEnd, bStart, bEnd) {
  return aStart < bEnd && bStart < aEnd;
}