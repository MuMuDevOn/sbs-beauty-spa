/**
 * This is the main entry point for all Cloud Functions.
 * It imports and exports all the functions from the other files in this folder.
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
  ...require("./payment"),              // initializePayment, verifyPayment, paystackWebhook

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
  ...require("./Bookingnotes"),          // addBookingNote, listBookingNotes
  ...require("./Notifications"),         // onBookingStatusChange, onPaymentCompleted (triggers)
  ...require("./Reviews"),               // submitReview
  ...require("./Contactmessages"),       // submitContactMessage, listContactMessages, markContactMessageHandled

  // Audit trail
  ...require("./Deletionlog"),         // listDeletionLog (logDeletion itself isn't a trigger — see that file's note)

   ...require("./AdminClients"),          // listClients, getClientDetails, setClientDisabled
};