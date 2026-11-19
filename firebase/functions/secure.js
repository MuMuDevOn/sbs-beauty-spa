/**
 * Drop-in replacement for require("firebase-functions/v2/https").
 *
 * It exports everything the original does, but its onCall() refuses anyone who
 * signed in with a password but did NOT finish the emailed-code step
 * (their token has no fresh "mfaAt" claim). Calls with no login at all are left
 * alone, so each function's own "must be signed in" check still decides.
 *
 * Safety switch: create firebase/functions/.env containing  MFA_ENFORCE=off
 * and redeploy to turn the check off (e.g. if SendGrid is down and nobody can log in).
 */

const https = require("firebase-functions/v2/https");
const { HttpsError } = https;

// "mfaAt" is set by verifyLoginCode (seconds). It must be at least as new as the
// token's own sign-in time ("auth_time"), so every fresh password login needs a fresh code.
function hasMfa(token) {
  return (
    token &&
    typeof token.mfaAt === "number" &&
    typeof token.auth_time === "number" &&
    token.mfaAt >= token.auth_time
  );
}

function guarded(handler) {
  return async (request) => {
    const enforce = process.env.MFA_ENFORCE !== "off";
    if (enforce && request.auth && !hasMfa(request.auth.token)) {
      throw new HttpsError(
        "permission-denied",
        "Your login needs to be confirmed again. Please log out and log in."
      );
    }
    return handler(request);
  };
}

function onCall(optsOrHandler, maybeHandler) {
  if (typeof optsOrHandler === "function") {
    return https.onCall(guarded(optsOrHandler));
  }
  return https.onCall(optsOrHandler, guarded(maybeHandler));
}

module.exports = { ...https, onCall, hasMfa };
