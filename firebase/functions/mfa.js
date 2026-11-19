/**
 * Email code at login (second step after the password).
 *
 *  - sendLoginCode   : emails a 6-digit code to the signed-in (password-checked) user
 *  - verifyLoginCode : checks the code the user typed
 *
 * Codes are stored only as a hash, expire after 10 minutes, allow 5 wrong tries,
 * and can't be re-requested more than once every 45 seconds (8 per hour).
 * The loginCodes collection is only ever touched by these functions (admin SDK).
 *
 * Email goes out through SendGrid, same as the booking emails (Notifications.js).
 */

const { onCall, HttpsError } = require("firebase-functions/v2/https");
const { defineSecret } = require("firebase-functions/params");
const admin = require("firebase-admin");
const crypto = require("crypto");
const sgMail = require("@sendgrid/mail");

if (admin.apps.length === 0) {
  admin.initializeApp();
}
const db = admin.firestore();

const SENDGRID_API_KEY = defineSecret("SENDGRID_API_KEY");
// Google Play reviewer account: that ONE email gets a fixed code instead of an emailed one
// (the reviewer can't read our inbox). Both values live in Firebase secrets, not in this file.
// To switch it off after review: delete/replace the REVIEW_EMAIL secret value with a blank-ish address.
const REVIEW_EMAIL = defineSecret("REVIEW_EMAIL");
const REVIEW_CODE = defineSecret("REVIEW_CODE");
const FROM_EMAIL =  "structsbsbeauty@gmail.com";// must be verified in SendGrid (same as Notifications.js)

const CODE_LIFETIME_MS = 10 * 60 * 1000;
const RESEND_COOLDOWN_MS = 45 * 1000;
const MAX_ATTEMPTS = 5;
const MAX_PER_HOUR = 8;
const HOUR_MS = 60 * 60 * 1000;

function requireUid(request) {
  const uid = request.auth && request.auth.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Please log in first.");
  return uid;
}

const hashCode = (uid, code) =>
  crypto.createHash("sha256").update(`${uid}:${code}`).digest("hex");

function maskEmail(email) {
  const [name, domain] = String(email).split("@");
  if (!domain) return "your email";
  const shown = name.length <= 2 ? name[0] : name.slice(0, 2);
  return `${shown}${"*".repeat(Math.max(3, name.length - shown.length))}@${domain}`;
}

function codeEmailHtml(code) {
  return `
  <div style="font-family:Arial,Helvetica,sans-serif;max-width:420px;margin:0 auto;padding:24px;color:#2B1B6B">
    <h2 style="margin:0 0 8px">SBS Beauty Spa</h2>
    <p style="margin:0 0 16px;color:#6E63A6">Your login code is:</p>
    <div style="font-size:34px;font-weight:bold;letter-spacing:8px;background:#EFE9FF;border-radius:14px;padding:16px;text-align:center">${code}</div>
    <p style="margin:16px 0 0;color:#6E63A6;font-size:13px">This code expires in 10 minutes. If you didn't try to log in, you can ignore this email and your account stays safe.</p>
  </div>`;
}

/**
 * sendLoginCode
 * Input: {} (caller must be signed in with their password)
 * Output: { sent: true, emailHint: "ab***@gmail.com" }
 */
exports.sendLoginCode = onCall({ secrets: [SENDGRID_API_KEY, REVIEW_EMAIL, REVIEW_CODE] }, async (request) => {
  const uid = requireUid(request);

  const user = await admin.auth().getUser(uid);
  if (!user.email) throw new HttpsError("failed-precondition", "This account has no email address.");

  // Reviewer account: store the fixed code (hashed like any other) and send no email.
  const reviewEmail = String(REVIEW_EMAIL.value() || "").trim().toLowerCase();
  const reviewCode = String(REVIEW_CODE.value() || "").trim();
  if (reviewEmail && /^[0-9]{6}$/.test(reviewCode) && user.email.toLowerCase() === reviewEmail) {
    const t = Date.now();
    await db.collection("loginCodes").doc(uid).set({
      codeHash: hashCode(uid, reviewCode),
      expiresAtMs: t + CODE_LIFETIME_MS,
      attempts: 0,
      sentAtMs: t,
      windowStartMs: t,
      windowCount: 1,
    });
    return { sent: true, emailHint: maskEmail(user.email) };
  }

  const ref = db.collection("loginCodes").doc(uid);
  const snap = await ref.get();
  const now = Date.now();
  const prev = snap.exists ? snap.data() : {};

  if (prev.sentAtMs && now - prev.sentAtMs < RESEND_COOLDOWN_MS) {
    throw new HttpsError("resource-exhausted", "Please wait a few seconds before asking for another code.");
  }

  let windowStartMs = prev.windowStartMs || now;
  let windowCount = prev.windowCount || 0;
  if (now - windowStartMs > HOUR_MS) {
    windowStartMs = now;
    windowCount = 0;
  }
  if (windowCount >= MAX_PER_HOUR) {
    throw new HttpsError("resource-exhausted", "Too many codes requested. Please try again in a while.");
  }

  const code = String(crypto.randomInt(0, 1000000)).padStart(6, "0");

  await ref.set({
    codeHash: hashCode(uid, code),
    expiresAtMs: now + CODE_LIFETIME_MS,
    attempts: 0,
    sentAtMs: now,
    windowStartMs,
    windowCount: windowCount + 1,
  });

  try {
    sgMail.setApiKey(SENDGRID_API_KEY.value());
    await sgMail.send({
      to: user.email,
      from: FROM_EMAIL,
      subject: `${code} is your SBS Beauty Spa login code`,
      html: codeEmailHtml(code),
    });
  } catch (err) {
    console.error("sendLoginCode email failed", err && err.response ? JSON.stringify(err.response.body) : err);
    await ref.delete();
    throw new HttpsError("unavailable", "We couldn't send the email right now. Please try again.");
  }

  return { sent: true, emailHint: maskEmail(user.email) };
});

/**
 * verifyLoginCode
 * Input: { code }
 * Output: { verified: true }  (throws with a friendly message otherwise)
 */
exports.verifyLoginCode = onCall(async (request) => {
  const uid = requireUid(request);
  const code = String((request.data && request.data.code) || "").trim();
  if (!/^[0-9]{6}$/.test(code)) {
    throw new HttpsError("invalid-argument", "Enter the 6-digit code from your email.");
  }

  const ref = db.collection("loginCodes").doc(uid);

  const outcome = await db.runTransaction(async (tx) => {
    const snap = await tx.get(ref);
    if (!snap.exists) return { ok: false, message: "No code was requested. Tap Resend code." };

    const d = snap.data();
    if (Date.now() > d.expiresAtMs) {
      tx.delete(ref);
      return { ok: false, message: "That code has expired. Tap Resend code." };
    }
    if ((d.attempts || 0) >= MAX_ATTEMPTS) {
      tx.delete(ref);
      return { ok: false, message: "Too many wrong tries. Tap Resend code to get a new one." };
    }

    const expected = Buffer.from(d.codeHash, "hex");
    const actual = Buffer.from(hashCode(uid, code), "hex");
    const match = expected.length === actual.length && crypto.timingSafeEqual(expected, actual);

    if (!match) {
      const used = (d.attempts || 0) + 1;
      tx.update(ref, { attempts: used });
      const left = MAX_ATTEMPTS - used;
      return {
        ok: false,
        message: left > 0 ? `That code isn't right. ${left} ${left === 1 ? "try" : "tries"} left.` : "Too many wrong tries. Tap Resend code to get a new one.",
      };
    }

    tx.delete(ref); // a code works once
    return { ok: true };
  });

  if (!outcome.ok) throw new HttpsError("failed-precondition", outcome.message);

  // Typing the code from their inbox proves they own the address.
  const user = await admin.auth().getUser(uid);
  if (!user.emailVerified) {
    try {
      await admin.auth().updateUser(uid, { emailVerified: true });
    } catch (err) {
      console.error("verifyLoginCode: couldn't mark email verified", err);
    }
  }

  await grantMfa(uid, user.customClaims);
  return { verified: true };
});

/**
 * Stamps the user's token with "mfaAt" (merged in, so admin / superAdmin claims survive).
 * The app then refreshes its token; every protected function and rule checks this stamp.
 */
async function grantMfa(uid, existingClaims) {
  try {
    await admin.auth().setCustomUserClaims(uid, {
      ...(existingClaims || {}),
      mfaAt: Math.floor(Date.now() / 1000),
    });
  } catch (err) {
    console.error("grantMfa failed", err);
    throw new HttpsError("internal", "We couldn't finish logging you in. Please try again.");
  }
}

/**
 * completeEmailSignup
 * For a brand-new account that has just tapped its verification link: lets the
 * app in without asking for a login code (they just proved they own the inbox).
 * Only works for accounts created in the last hour that haven't been through MFA yet.
 */
exports.completeEmailSignup = onCall(async (request) => {
  const uid = requireUid(request);
  const user = await admin.auth().getUser(uid);

  if (!user.emailVerified) {
    throw new HttpsError("failed-precondition", "We can't see the verification yet. Open the link in your email first, then tap again.");
  }

  const claims = user.customClaims || {};
  const createdMs = Date.parse(user.metadata.creationTime);
  if (claims.mfaAt || !Number.isFinite(createdMs) || Date.now() - createdMs > 60 * 60 * 1000) {
    throw new HttpsError("permission-denied", "Please log in with your password to get an email code.");
  }

  await grantMfa(uid, claims);
  return { ok: true };
});