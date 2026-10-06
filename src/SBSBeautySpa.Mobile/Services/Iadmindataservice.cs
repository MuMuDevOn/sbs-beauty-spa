using NodaTime;
using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{
    // A summary of everything important for the admin dashboard.
    // Shows at a glance:
    //   - How many upcoming bookings there are
    //   - How much money came in this month
    //   - How many bookings are pending
    //   - A short list of recent bookings
    public record AdminDashboardSummary(
        int UpcomingBookingsCount,
        decimal RevenueThisMonth,
        int PendingCount,
        List<Booking> RecentBookings
    );

    // This service is for admins only.
    // It reads bookings from ALL clients — not just the current user's.
    //
    // This is different from IFirestoreService, which only lets the
    // signed-in user see their own data.
    //
    // There are two ways to build this:
    //
    //   Option A: Read Firestore directly
    //     Use a security rule like:
    //       allow read: if request.auth.token.admin == true;
    //     This means only users with the "admin" role can read the data.
    //     (The role is set by the adminClaims.js Cloud Function.)
    //
    //   Option B: Use Cloud Functions
    //     Call a Cloud Function that checks the admin role on the server,
    //     the same way getAvailableSlots and createBooking do.
    //
    // This interface doesn't care which one you pick — both work.
    // The choice is up to whoever owns the Firestore rules and schema.
    public interface IAdminDataService
    {
        // Gets the summary numbers for the admin dashboard.
        Task<AdminDashboardSummary> GetDashboardSummaryAsync(CancellationToken ct = default);

        // Gets a filtered list of bookings (for the admin bookings screen).
        // You can filter by:
        //   - status (pending, confirmed, completed, cancelled)
        //   - date range (from and to)
        Task<List<Booking>> GetBookingsAsync(
            BookingStatus? status = null,
            LocalDate? from = null,
            LocalDate? to = null,
            CancellationToken ct = default);
    }
}