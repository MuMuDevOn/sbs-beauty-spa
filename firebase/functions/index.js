/**
 * index.js
 * -----------------------------------------------------------------------
 * This is the main entry point for all Cloud Functions.
 *
 * How it works:
 *   - Each feature (payments, bookings, etc.) lives in its own file.
 *   - That file exports one or more Cloud Functions.
 *   - This file just pulls them all together into one big object.
 *   - When you run "firebase deploy --only functions", Firebase scans
 *     this file and deploys everything it finds.
 *
 * If you already have your own index.js with functions:
 *   - Just add more `...require("./yourFile")` lines below.
 *   - Don't replace this file — just add to it.
 *
 * Firebase initialization:
 *   - admin.initializeApp() runs once here.
 *   - Every other file also checks `if (admin.apps.length === 0)` before
 *     calling it, so each file works on its own too.
 *   - Calling initializeApp() twice would crash — the guard prevents that.
 * -----------------------------------------------------------------------
 */

const admin = require("firebase-admin");

// Only initialize Firebase if it hasn't been initialized yet.
if (admin.apps.length === 0) {
  admin.initializeApp();
}

// Pull in all the functions from each feature file.
// The `...` spreads them into one big object that Firebase deploys.
module.exports = {
  // Booking and availability
  ...require("./availability"),          // getAvailableSlots, createBooking
  ...require("./Adminavailability"),     // getBusinessHours, setBusinessHours, listBlockedTime, setBlockedTime, deleteBlockedTime
  ...require("./Adminbookings"),         // updateBookingStatus

  // Payments (Paystack)
  ...require("./payments"),              // initializePayment, verifyPayment, paystackWebhook

  // Admin identity and access
  ...require("./Adminclaims"),           // setAdminClaim, setSuperAdminClaim, listAdmins

  // Service catalog
  ...require("./Adminservices"),         // createService, updateService, setServiceActive, deleteService
  ...require("./Servicecategories"),     // createServiceCategory, updateServiceCategory, setServiceCategoryActive

  // Shop (products, cart checkout, orders)
  ...require("./Shop"),

  // Gallery
  ...require("./Gallery"),               // createGalleryCategory, addGalleryImage, deleteGalleryImage

  // Client profile
  ...require("./Clientprofile"),         // getMyPreferences, setMyPreferences, addClientNote, listClientNotes

  // Business settings
  ...require("./Businesssettings"),      // getBusinessSettingsPublic/Private, updateBusinessSettingsPublic/Private

  // Notes, notifications, reviews, contact form
  ...require("./Nookingnotes"),          // addBookingNote, listBookingNotes
  ...require("./Notifications"),         // onBookingStatusChange, onPaymentCompleted (triggers)
  ...require("./Reviews"),               // submitReview
  ...require("./Contactmessages"),       // submitContactMessage, listContactMessages, markContactMessageHandled

  // Audit trail
  ...require("./Deletionlog"),         // listDeletionLog (logDeletion itself isn't a trigger — see that file's note)
};