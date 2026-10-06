namespace SBSBeautySpa.Mobile.Models
{
    // Represents a gallery category (like "Nail Designs", "Lashes").
    public class GalleryCategory
    {
        // The unique ID of this category.
        public string Id { get; set; } = string.Empty;

        // The category name shown to users.
        public string Name { get; set; } = string.Empty;

        // Whether this category is currently shown.
        public bool IsActive { get; set; } = true;
    }

    // One image in the gallery.
    public class GalleryImage
    {
        // The unique ID of this image.
        public string Id { get; set; } = string.Empty;

        // Which category this image belongs to.
        public string CategoryId { get; set; } = string.Empty;

        // The URL of the actual image file.
        // (The image itself is stored in Firebase Storage.)
        public string ImageUrl { get; set; } = string.Empty;

        // An optional title for the image.
        public string Title { get; set; } = string.Empty;

        // An optional description of the image.
        public string Description { get; set; } = string.Empty;
    }

    // Represents a client's review of a service.
    //
    // Reviews are only created via submitReview (a Cloud Function).
    // Each completed booking can be reviewed once.
    // Reading reviews is public.
    public class Review
    {
        // The unique ID of this review.
        public string Id { get; set; } = string.Empty;

        // The booking this review is for.
        public string BookingId { get; set; } = string.Empty;

        // The client who wrote the review.
        public string ClientId { get; set; } = string.Empty;

        // The service that was reviewed.
        public string ServiceId { get; set; } = string.Empty;

        // The rating from 1 to 5.
        public int Rating { get; set; }

        // The review text.
        public string Comment { get; set; } = string.Empty;

        // When the review was written.
        public DateTime? CreatedAt { get; set; }
    }

    // The possible states a contact message can be in.
    public enum ContactMessageStatus
    {
        New,        // Not yet looked at
        Handled     // Admin has responded or dealt with it
    }

    // Represents a message from the public contact form.
    //
    // IMPORTANT: No sign-in required to submit one.
    // This is the public "get in touch" form.
    public class ContactMessage
    {
        // The unique ID of this message.
        public string Id { get; set; } = string.Empty;

        // The sender's name.
        public string Name { get; set; } = string.Empty;

        // The sender's email.
        public string Email { get; set; } = string.Empty;

        // The sender's phone (optional).
        public string Phone { get; set; } = string.Empty;

        // The subject of the message.
        public string Subject { get; set; } = string.Empty;

        // The message body.
        public string Message { get; set; } = string.Empty;

        // Whether the message has been handled.
        public ContactMessageStatus Status { get; set; } = ContactMessageStatus.New;

        // When the message was sent.
        public DateTime? CreatedAt { get; set; }
    }

    // The types of notifications the system sends.
    public enum NotificationType
    {
        BookingStatus,     // Booking status changed
        PaymentCompleted,  // Payment was received
        BookingReminder    // Reminder before an appointment
    }

    // The channels a notification can go through.
    public enum NotificationChannel
    {
        InApp,   // Shows in the app's notification centre
        Email,   // Sent as an email
        Push     // Sent as a push notification
    }

    // The possible states of a notification.
    public enum NotificationStatus
    {
        Queued,  // Waiting to be sent
        Sent,    // Successfully sent
        Failed   // Failed to send
    }

    // Represents one notification sent to a client.
    //
    // Notifications are only created by the server (Cloud Functions),
    // never by a client writing directly.
    //
    // In-app notifications are always Status = Sent (the write either
    // succeeds or the function throws — no partial state).
    //
    // Email and push notifications go through a real lifecycle:
    //   Queued → Sent or Failed
    // with Attempts and LastError tracked on failure.
    public class Notification
    {
        // The unique ID of this notification.
        public string Id { get; set; } = string.Empty;

        // Which client this is for.
        public string ClientId { get; set; } = string.Empty;

        // What type of notification this is.
        public NotificationType Type { get; set; }

        // Which channel it was sent through.
        public NotificationChannel Channel { get; set; }

        // The current status.
        public NotificationStatus Status { get; set; }

        // The ID of whatever this notification is about
        // (like the booking ID or payment ID).
        public string RelatedId { get; set; } = string.Empty;

        // The message shown.
        public string Message { get; set; } = string.Empty;

        // Whether the client has read it (for in-app notifications).
        public bool IsRead { get; set; }

        // How many times we've tried to send it.
        public int Attempts { get; set; }

        // The last error (if any).
        public string? LastError { get; set; }

        // When the notification was created.
        public DateTime? CreatedAt { get; set; }

        // When we last tried to send it.
        public DateTime? LastAttemptAt { get; set; }
    }

    // Internal staff notes about a specific appointment.
    // NEVER shown to the client.
    public class BookingNote
    {
        // The unique ID of this note.
        public string Id { get; set; } = string.Empty;

        // Which booking this note is about.
        public string BookingId { get; set; } = string.Empty;

        // The note text.
        public string Note { get; set; } = string.Empty;

        // When the note was written.
        public DateTime? CreatedAt { get; set; }
    }

    // An entry in the deletion audit log.
    // Written by the logDeletion helper whenever an admin hard-deletes
    // something (services, products, gallery images, etc.).
    public class DeletionLogEntry
    {
        // The unique ID of this log entry.
        public string Id { get; set; } = string.Empty;

        // What kind of thing was deleted (service, product, etc.).
        public string EntityType { get; set; } = string.Empty;

        // The ID of the deleted item.
        public string EntityId { get; set; } = string.Empty;

        // Who deleted it (their user ID).
        public string DeletedBy { get; set; } = string.Empty;

        // Optional reason for the deletion.
        public string Reason { get; set; } = string.Empty;

        // When the deletion happened.
        public DateTime? DeletedAt { get; set; }
    }
}