using System.Globalization;

namespace SBSBeautySpa.Mobile.Views
{
    public partial class BookingConfirmationPage : ContentPage, IQueryAttributable
    {
        public BookingConfirmationPage() => InitializeComponent();

        // Receives the booking details from the Review page.
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            string Text(string key) => query.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;

            var name = Text("ServiceName");
            ServiceNameLabel.Text = string.IsNullOrEmpty(name) ? "Your service" : name;

            var date = Text("DateText");
            var time = Text("TimeText");
            var duration = Text("DurationText");
            var when = string.IsNullOrEmpty(time) ? date : $"{date} at {time}";
            if (!string.IsNullOrEmpty(duration)) when += $" ({duration})";
            WhenLabel.Text = when;

            var addOns = Text("AddOns");
            AddOnsLabel.IsVisible = !string.IsNullOrEmpty(addOns);
            AddOnsLabel.Text = $"Add-Ons: {addOns}";

            // Only shown once a real booking reference exists.
            var reference = Text("Reference");
            ReferenceLabel.IsVisible = !string.IsNullOrEmpty(reference);
            ReferenceLabel.Text = $"Reference Number: #{reference}";

            var paidType = Text("PaidType");
            var paid = query.TryGetValue("PaidAmount", out var pa) && pa is decimal p ? p
                     : query.TryGetValue("Deposit", out var d) && d is decimal dep ? dep : 0m;
            var balance = query.TryGetValue("BalanceDue", out var bd) && bd is decimal b ? b : 0m;

            DepositLabel.Text = (paidType == "full" ? "Paid in Full: R" : "Deposit Paid: R")
                + paid.ToString("0.00", CultureInfo.InvariantCulture) + " (Confirmed)";

            BalanceLabel.IsVisible = paidType != "full" && balance > 0;
            BalanceLabel.Text = "Balance to pay in-store on the day: R" + balance.ToString("0.00", CultureInfo.InvariantCulture);
        }

        // The booking is finished, so back goes home rather than into the booking steps.
        private async void OnBackTapped(object sender, TappedEventArgs e)
            => await Shell.Current.GoToAsync("//HomePage");

        private async void OnViewBookingsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//BookingPage");
            await Shell.Current.GoToAsync("BookingHistoryPage");
        }

        private async void OnReturnHomeClicked(object sender, EventArgs e)
            => await Shell.Current.GoToAsync("//HomePage");
    }
}