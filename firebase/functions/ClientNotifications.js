/**
 * The client's own notification list (the bell on the home page).
 * Reads the in-app notifications that other functions write (booking updates,
 * payments, reminders, replies to Contact Us messages).
 */

const { onCall, HttpsError } = require("./secure");
const admin = require("firebase-admin");

if (admin.apps.length === 0) {
  admin.initializeApp();
}
const db = admin.firestore();

function requireUid(request) {
  if (!request.auth) throw new HttpsError("unauthenticated", "Please log in first.");
  return request.auth.uid;
}

// Newest first, in-app only. Filtering in code avoids needing a Firestore index.
async function loadMine(uid) {
  const snap = await db.collection("notifications").where("clientId", "==", uid).get();
  const iso = (t) => (t && t.toDate ? t.toDate().toISOString() : null);

  return snap.docs
    .map((d) => ({ id: d.id, ...d.data() }))
    .filter((n) => n.channel === "inApp")
    .sort((a, b) => (b.createdAt ? b.createdAt.toMillis() : 0) - (a.createdAt ? a.createdAt.toMillis() : 0))
    .slice(0, 50)
    .map((n) => ({
      id: n.id,
      type: n.type || "",
      message: n.message || "",
      isRead: n.isRead === true,
      createdAt: iso(n.createdAt),
    }));
}

/** listMyNotifications  Input: {}  Output: { notifications, unreadCount } */
exports.listMyNotifications = onCall(async (request) => {
  const uid = requireUid(request);
  const notifications = await loadMine(uid);
  return { notifications, unreadCount: notifications.filter((n) => !n.isRead).length };
});

/** markMyNotificationsRead  Input: {}  Output: { updated } */
exports.markMyNotificationsRead = onCall(async (request) => {
  const uid = requireUid(request);
  const snap = await db.collection("notifications").where("clientId", "==", uid).get();

  const unread = snap.docs.filter((d) => d.data().channel === "inApp" && d.data().isRead !== true);
  const batch = db.batch();
  unread.forEach((d) => batch.update(d.ref, { isRead: true }));
  if (unread.length > 0) await batch.commit();

  return { updated: unread.length };
});
