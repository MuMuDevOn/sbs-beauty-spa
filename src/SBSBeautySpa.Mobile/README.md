# SBSBeautySpa.Mobile

.NET MAUI client + admin application. Not yet scaffolded — run this once the .NET 8 SDK + MAUI workload are installed locally (this environment doesn't have `dotnet` available, so the actual project file couldn't be generated here):

```
dotnet workload install maui
dotnet new maui -n SBSBeautySpa.Mobile
```

Then move the generated files into this folder (or run the command from here directly) and delete this placeholder README.

## Planned structure

- **Models/** — `Client`, `Admin`, `Service`, `AvailabilitySlot`, `Booking`, `Payment`, `NotificationLog` (matches the CRC cards in `../../docs/diagrams/`)
- **ViewModels/** — one per screen, MVVM pattern, bindable to the Views
- **Views/** — XAML pages: Login/Signup, Home/Catalogue, Booking Calendar, Checkout, Client Dashboard (Bookings/Profile), Admin Dashboard (Overview/Calendar/Bookings/Catalogue/Payments/Clients/Notifications)
- **Services/** — thin wrappers around the Firebase SDK (`FirebaseAuthService`, `FirestoreService`, `PaymentService`, `NotificationService`) so the ViewModels never talk to Firebase directly — keeps the app testable and matches the layered-architecture requirement in the proposal's Maintainability NFR.
- **Resources/** — colours, styles, fonts matching the palette in the proposal (Deep Navy `#1B2A4A`, Stormy Blue `#4D6893`, Dusty Periwinkle `#7C93C4`, Ice Blue `#DCE8F5`)
