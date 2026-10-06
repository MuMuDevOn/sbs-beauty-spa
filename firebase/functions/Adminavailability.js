/**
 * Adminavailability.js
 * -----------------------------------------------------------------------
 * This file is the write-side counterpart to availability.js.
 *
 * availability.js READS businessHours/{weekday} and blockedTime docs to
 * figure out what slots are free. This file is what CREATES and MANAGES
 * those docs.
 *
 * Without this file, every day would resolve to "closed" (no hours doc
 * = no slots). These five functions are how the admin:
 *   - Sets normal opening hours once
 *   - Blocks off one-off periods (public holidays, staff leave, etc.)
 *
 * Nothing here is hardcoded on the client — it all lives in Firestore,
 * and getAvailableSlots picks it up on the next call.
 *
 * Authorization: every write here checks the admin claim
 * (`request.auth.token.admin === true`).
 * -----------------------------------------------------------------------
 */

const { onCall, HttpsError } = require("firebase-functions/v2/https");
const admin = require("firebase-admin");

// Initialize Firebase if not already done.
if (admin.apps.length === 0) {
  admin.initializeApp();
}
const db = admin.firestore();

// Regex to check time format like "09:00" or "17:30".
const TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/;

// Helper: throws an error if the caller isn't an admin.
function assertIsAdmin(request) {
  if (!request.auth) {
    throw new HttpsError("unauthenticated", "Must be signed in.");
  }
  if (request.auth.token.admin !== true) {
    throw new HttpsError("permission-denied", "Admin access required.");
  }
}

// Helper: throws an error if a time string isn't in "HH:mm" format.
function assertValidTime(label, value) {
  if (!TIME_PATTERN.test(value)) {
    throw new HttpsError("invalid-argument", `${label} must be in HH:mm 24-hour format, e.g. "09:00".`);
  }
}

/**
 * getBusinessHours
 * Output: { hours: [{ weekday, isClosed, openTime, closeTime }, ...] }
 *
 * ANY signed-in user can read this (clients benefit from seeing store
 * hours too). It's the admin WRITES below that are locked down.
 *
 * Weekday: 0 = Sunday, 1 = Monday, ..., 6 = Saturday.
 * Only configured days are included.
 */
exports.getBusinessHours = onCall(async (request) => {
  if (!request.auth) {
    throw new HttpsError("unauthenticated", "Must be signed in.");
  }

  // Load all business hours from Firestore.
  const snap = await db.collection("businessHours").get();

  // Convert to our shape, adding the weekday from the document ID.
  // Sort by weekday (0-6).
  const hours = snap.docs
    .map((d) => ({ weekday: Number(d.id), ...d.data() }))
    .sort((a, b) => a.weekday - b.weekday);

  return { hours };
});

/**
 * setBusinessHours
 * Input:  { hours: [{ weekday: 0-6, isClosed: bool, openTime?: "HH:mm", closeTime?: "HH:mm" }, ...] }
 * Output: { updated: <number> }
 *
 * Admin only. Upserts one or more weekdays in a single batched write.
 */
exports.setBusinessHours = onCall(async (request) => {
  assertIsAdmin(request);

  const { hours } = request.data || {};
  if (!Array.isArray(hours) || hours.length === 0) {
    throw new HttpsError("invalid-argument", "hours must be a non-empty array.");
  }

  // Use a batch write so all weekdays update together (all or nothing).
  const batch = db.batch();

  for (const entry of hours) {
    const { weekday, isClosed, openTime, closeTime } = entry;

    // Check the weekday is valid.
    if (!Number.isInteger(weekday) || weekday < 0 || weekday > 6) {
      throw new HttpsError("invalid-argument", "weekday must be an integer 0 (Sunday) to 6 (Saturday).");
    }

    // If the day isn't closed, check the times.
    if (!isClosed) {
      assertValidTime("openTime", openTime);
      assertValidTime("closeTime", closeTime);
      if (openTime >= closeTime) {
        throw new HttpsError("invalid-argument", `Day ${weekday}: openTime must be before closeTime.`);
      }
    }

    // Update the document for this weekday.
    const docRef = db.collection("businessHours").doc(String(weekday));
    batch.set(
      docRef,
      {
        isClosed: !!isClosed,
        openTime: isClosed ? null : openTime,
        closeTime: isClosed ? null : closeTime,
        updatedAt: admin.firestore.FieldValue.serverTimestamp(),
        updatedBy: request.auth.uid,
      },
      { merge: true }
    );
  }

  await batch.commit();
  return { updated: hours.length };
});

/**
 * listBlockedTime
 * Input:  { fromDate?: "YYYY-MM-DD", toDate?: "YYYY-MM-DD" }
 * Output: { blocks: [{ id, start, end, reason }, ...] }
 *
 * Admin only. Powers the "upcoming blocked periods" list on the admin
 * availability screen.
 */
exports.listBlockedTime = onCall(async (request) => {
  assertIsAdmin(request);

  const { fromDate, toDate } = request.data || {};

  // Start with a query ordered by start time.
  let query = db.collection("blockedTime").orderBy("start", "asc");

  // Optional date range filter.
  if (fromDate) {
    query = query.where("end", ">=", new Date(`${fromDate}T00:00:00Z`));
  }
  if (toDate) {
    query = query.where("start", "<=", new Date(`${toDate}T23:59:59Z`));
  }

  const snap = await query.get();

  // Convert to our shape.
  const blocks = snap.docs.map((d) => {
    const data = d.data();
    return {
      id: d.id,
      start: data.start.toDate().toISOString(),
      end: data.end.toDate().toISOString(),
      reason: data.reason || "",
    };
  });

  return { blocks };
});

/**
 * setBlockedTime
 * Input:  { start: ISO datetime, end: ISO datetime, reason: string }
 * Output: { blockedTimeId }
 *
 * Admin only. Creates one blocked period — a public holiday, staff
 * leave, equipment downtime, etc.
 *
 * getAvailableSlots excludes any candidate slot that overlaps this range.
 */
exports.setBlockedTime = onCall(async (request) => {
  assertIsAdmin(request);

  const { start, end, reason } = request.data || {};

  // Parse the dates.
  const startDate = start ? new Date(start) : null;
  const endDate = end ? new Date(end) : null;

  // Check the dates are valid.
  if (!startDate || isNaN(startDate.getTime()) || !endDate || isNaN(endDate.getTime())) {
    throw new HttpsError("invalid-argument", "start and end must be valid ISO datetimes.");
  }
  if (startDate >= endDate) {
    throw new HttpsError("invalid-argument", "start must be before end.");
  }

  // Save the blocked period.
  const docRef = await db.collection("blockedTime").add({
    start: admin.firestore.Timestamp.fromDate(startDate),
    end: admin.firestore.Timestamp.fromDate(endDate),
    reason: reason || "",
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
    createdBy: request.auth.uid,
  });

  return { blockedTimeId: docRef.id };
});

/**
 * deleteBlockedTime
 * Input: { blockedTimeId: string }
 * Output: { deleted: true }
 *
 * Admin only. Removes a blocked period — like a mistaken entry, or
 * leave that got cancelled. Frees those slots back up immediately.
 */
exports.deleteBlockedTime = onCall(async (request) => {
  assertIsAdmin(request);

  const { blockedTimeId } = request.data || {};
  if (!blockedTimeId) {
    throw new HttpsError("invalid-argument", "blockedTimeId is required.");
  }

  const docRef = db.collection("blockedTime").doc(blockedTimeId);
  const snap = await docRef.get();
  if (!snap.exists) {
    throw new HttpsError("not-found", "Blocked period not found.");
  }

  await docRef.delete();
  return { deleted: true };
});