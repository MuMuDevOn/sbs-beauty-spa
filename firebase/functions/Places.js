/**
 * Address search for the delivery address form (Google Places API, "New").
 *
 * The Google key never leaves the server: the app calls these two functions and
 * they talk to Google. Store the key once with:
 *     firebase functions:secrets:set GOOGLE_MAPS_API_KEY
 *
 *   placesAutocomplete  { input, sessionToken }  -> { suggestions: [{ placeId, mainText, secondaryText, description }] }
 *   placesDetails       { placeId, sessionToken } -> { streetAddress, suburb, city, province, postalCode, formattedAddress }
 *
 * Both need a signed-in user (so strangers can't run up the Google bill).
 */

const { onCall, HttpsError } = require("./secure");
const { defineSecret } = require("firebase-functions/params");

const GOOGLE_MAPS_API_KEY = defineSecret("GOOGLE_MAPS_API_KEY");

function requireUid(request) {
  if (!request.auth) throw new HttpsError("unauthenticated", "Please log in first.");
  return request.auth.uid;
}

// Google wants a short URL-safe token that groups one search into one billable session.
function cleanToken(t) {
  const s = String(t || "");
  return /^[A-Za-z0-9_-]{8,36}$/.test(s) ? s : undefined;
}

// Picks the pieces we store out of Google's address components.
function parseComponents(components) {
  const list = Array.isArray(components) ? components : [];
  const get = (...types) => {
    for (const type of types) {
      const c = list.find((x) => Array.isArray(x.types) && x.types.includes(type));
      if (c && c.longText) return c.longText;
    }
    return "";
  };

  const number = get("street_number");
  const route = get("route");
  let street = `${number} ${route}`.trim();
  if (!street) street = get("premise", "subpremise");

  return {
    streetAddress: street,
    suburb: get("sublocality_level_1", "sublocality", "neighborhood"),
    city: get("locality", "administrative_area_level_2"),
    province: get("administrative_area_level_1"),
    postalCode: get("postal_code"),
  };
}

exports.placesAutocomplete = onCall({ secrets: [GOOGLE_MAPS_API_KEY] }, async (request) => {
  requireUid(request);

  const text = String((request.data && request.data.input) || "").trim();
  if (text.length < 3) return { suggestions: [] };
  if (text.length > 200) throw new HttpsError("invalid-argument", "That search is too long.");

  const res = await fetch("https://places.googleapis.com/v1/places:autocomplete", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Goog-Api-Key": GOOGLE_MAPS_API_KEY.value(),
    },
    body: JSON.stringify({
      input: text,
      sessionToken: cleanToken(request.data.sessionToken),
      includedRegionCodes: ["za"],
      languageCode: "en",
    }),
  });

  if (!res.ok) {
    console.error("Places autocomplete failed:", res.status, await res.text());
    throw new HttpsError("unavailable", "Address search isn't available right now.");
  }

  const body = await res.json();
  const suggestions = (body.suggestions || [])
    .filter((s) => s.placePrediction && s.placePrediction.placeId)
    .slice(0, 5)
    .map((s) => {
      const p = s.placePrediction;
      return {
        placeId: p.placeId,
        mainText: (p.structuredFormat && p.structuredFormat.mainText && p.structuredFormat.mainText.text) || (p.text && p.text.text) || "",
        secondaryText: (p.structuredFormat && p.structuredFormat.secondaryText && p.structuredFormat.secondaryText.text) || "",
        description: (p.text && p.text.text) || "",
      };
    });

  return { suggestions };
});

exports.placesDetails = onCall({ secrets: [GOOGLE_MAPS_API_KEY] }, async (request) => {
  requireUid(request);

  const placeId = String((request.data && request.data.placeId) || "");
  if (!/^[A-Za-z0-9_-]{10,300}$/.test(placeId)) {
    throw new HttpsError("invalid-argument", "That address couldn't be read. Please pick another.");
  }

  const token = cleanToken(request.data.sessionToken);
  const url =
    `https://places.googleapis.com/v1/places/${encodeURIComponent(placeId)}` +
    `?languageCode=en${token ? "&sessionToken=" + token : ""}`;

  const res = await fetch(url, {
    headers: {
      "X-Goog-Api-Key": GOOGLE_MAPS_API_KEY.value(),
      "X-Goog-FieldMask": "addressComponents,formattedAddress",
    },
  });

  if (!res.ok) {
    console.error("Places details failed:", res.status, await res.text());
    throw new HttpsError("unavailable", "Address search isn't available right now.");
  }

  const body = await res.json();
  return { ...parseComponents(body.addressComponents), formattedAddress: body.formattedAddress || "" };
});

