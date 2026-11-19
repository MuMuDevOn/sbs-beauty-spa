namespace SBSBeautySpa.Mobile.Views;

// The text of the Terms, Privacy Policy and Cancellation Policy.
// One place to edit; the About page, Sign Up and Booking review all show it.
public record LegalSection(string Heading, string Body);

public record LegalDocument(string Title, IReadOnlyList<LegalSection> Sections);

public static class LegalContent
{
    public const string LastUpdated = "21 October 2026";

    public static LegalDocument Get(string? key) => key?.ToLowerInvariant() switch
    {
        "privacy" => Privacy,
        "cancellation" => Cancellation,
        _ => Terms,
    };

    public static readonly LegalDocument Terms = new("Terms and Conditions", new[]
    {
        new LegalSection("1. Introduction",
            "Welcome to SBS Beauty Spa! Our app allows you to browse beauty services and products, book appointments, make payments and manage your bookings. By using the app, you agree to these Terms and Conditions."),
        new LegalSection("2. User Accounts",
            "Users must provide accurate information when creating an account and keep their login details secure. Users may not access other people's accounts or use the app for unlawful activities."),
        new LegalSection("3. User Roles",
            "The app has three user roles:\n• Client/User: Can manage their profile, book appointments, make payments and view booking history.\n• Admin/Employee: Can manage appointments, availability and relevant client information.\n• Super Admin: The business owner, with access to employee management, user management, business reports and important settings. This account cannot be created by ordinary users or employees."),
        new LegalSection("4. Bookings, Rescheduling and Cancellations",
            "Clients are responsible for selecting the correct service, date and time. Appointments are confirmed once the app or SBS Beauty Spa confirms the booking.\n\nRescheduling: an appointment can only be rescheduled at least 24 hours before its scheduled start time. Within 24 hours of the appointment it can no longer be rescheduled through the app. Please contact SBS Beauty Spa directly.\n\nPlease arrive on time and notify us if you need to cancel. Any cancellation deadlines or charges will be communicated before booking. If we need to cancel or reschedule an appointment, we will notify you as soon as reasonably possible. Please also read our Cancellation Policy."),
        new LegalSection("5. Payments and Refunds",
            "Service and product prices will be communicated before payment. Some appointments may require a deposit, and any applicable conditions will be explained before booking.\n\nRefunds: a deposit can be refunded if you ask within 24 hours of paying; after that it is non-refundable. If you paid in full, the amount minus the deposit can still be refunded days later, as long as your service has not been done. Please see our Cancellation Policy. Refunds, cancellations and payment disputes will be handled fairly and in accordance with our applicable policies and South African law."),
        new LegalSection("6. Services and Products",
            "Clients should inform us of relevant allergies or sensitivities before a beauty treatment. Nail designs shown in the app are examples, and final results may vary.\n\nFor product orders, clients must provide correct delivery details. Delivery times may vary when third-party couriers are used. Please contact us if an order is missing, damaged or incorrect."),
        new LegalSection("7. Notifications and Privacy",
            "We may send booking confirmations, reminders, payment updates and order notifications through the app or email.\n\nWe collect personal information needed to manage accounts, appointments, payments and orders. We take reasonable steps to protect this information and handle it in accordance with South African privacy laws, including POPIA. Please also read our Privacy Policy."),
        new LegalSection("8. Acceptable Use",
            "Users must not misuse the app, access restricted information or interfere with its security. We may restrict accounts involved in fraud, unauthorised access or serious misuse where reasonably necessary.\n\nWe aim to keep the app available but cannot guarantee uninterrupted service."),
        new LegalSection("9. Changes to These Terms",
            "SBS Beauty Spa may update these Terms and Conditions when necessary. Updated terms will be made available through the app, with the date of the latest update."),
        new LegalSection("10. Governing Law",
            "These Terms and Conditions are governed by the laws of the Republic of South Africa. Clients may contact us directly to resolve any concerns or complaints."),
        new LegalSection("11. Contact Us",
            "For assistance or questions, please use the Contact Us page in the app or email structsbsbeauty@gmail.com."),
        new LegalSection("12. Acceptance",
            "By creating an account or accepting these terms in the app, you confirm that you have read and agree to these Terms and Conditions, subject to your rights under applicable law."),
    });

    public static readonly LegalDocument Privacy = new("Privacy Policy", new[]
    {
        new LegalSection("Your privacy",
            "SBS Beauty Spa respects your privacy and is committed to protecting your personal information in accordance with the Protection of Personal Information Act 4 of 2013 (POPIA) of South Africa."),
        new LegalSection("What we collect and why",
            "When you use the app, we may collect personal information such as your name, contact details, booking history, payment records and product orders. This information is used to manage your account, process bookings and payments, provide customer support and improve our services."),
        new LegalSection("How we protect it",
            "We take reasonable steps to protect your personal information against unauthorised access, loss or misuse. Your information may be shared with trusted service providers, such as payment processors, email services and courier companies, when necessary to provide our services."),
        new LegalSection("Your rights",
            "In accordance with POPIA, you have the right to request access to your personal information, ask for incorrect information to be corrected and raise concerns about how your information is handled. You may also exercise other rights available to you under the Act."),
        new LegalSection("How long we keep it",
            "We only retain personal information for as long as reasonably necessary for its intended purpose or as required by law."),
        new LegalSection("Your acknowledgement",
            "By using the app, you acknowledge that your personal information will be processed for the purposes explained above, subject to applicable law. Please contact SBS Beauty Spa through the Contact Us page or at structsbsbeauty@gmail.com if you have questions or concerns about your privacy."),
    });

    public static readonly LegalDocument Cancellation = new("Cancellation Policy", new[]
    {
        new LegalSection("Rescheduling",
            "You can reschedule an appointment in the app only if your current appointment is at least 24 hours away. Within 24 hours of your appointment, rescheduling in the app is no longer possible. Please contact SBS Beauty Spa directly."),
        new LegalSection("Cancelling",
            "If you need to cancel, please let us know as early as possible using the Contact Us page in the app or by email at structsbsbeauty@gmail.com. Any cancellation deadlines or charges will be communicated before you book."),
        new LegalSection("Deposits",
            "Some services need a deposit. The deposit amount and any conditions are shown on the review screen before you pay."),
        new LegalSection("If we need to change your appointment",
            "If we need to cancel or reschedule an appointment, we will notify you as soon as reasonably possible."),
        new LegalSection("Refunds",
            "Deposit: you can ask for your deposit to be refunded within 24 hours of paying. After 24 hours the deposit is non-refundable.\n\nPaying in full: the deposit part follows the same rule, so it is not refunded after 24 hours. The rest of the payment (the full amount minus the deposit) can still be refunded days later, as long as your service has not been done.\n\nOnce your service has been done, it can no longer be refunded, except where South African law says otherwise.\n\nTo ask for a refund, contact us through the Contact Us page in the app or email structsbsbeauty@gmail.com. Refunds, cancellations and payment disputes are handled fairly and in accordance with South African law."),
    });
}