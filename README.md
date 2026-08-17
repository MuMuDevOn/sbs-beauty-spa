# SBS Beauty Spa — Booking & Management App

A centralised booking and management mobile application for SBS Beauty Spa, replacing manual WhatsApp/Instagram-based scheduling with a structured, secure booking platform.

Built for **ITPJA3-B34 Project** — Group 20, **Struct Defenders**.

## Team

| Role | Name |
|---|---|
| Project Manager, Backend Developer | Mutsa Kembo |
| UX Designer | Keneilwe Machete |
| Full Stack Developer | Mihle Dyasi |
| Front-End Developer | Thumeka Msebenzi |
| Database Designer | Tinevimbo Albertina Dzimbiri |

## Tech stack

| Layer | Technology |
|---|---|
| Frontend (Client & Admin) | .NET MAUI (C#) |
| Backend | Firebase Cloud Functions |
| Database | Cloud Firestore (NoSQL) |
| Authentication | Firebase Authentication |
| Push notifications | Firebase Cloud Messaging (FCM) + Apple Push Notification service (APNs) |
| Email | Transactional email API (SendGrid/SMTP) via Cloud Function |
| Payments | PayGate (via Firebase Cloud Functions) |
| Design | Figma |

## Repo structure

```
├── src/
│   └── SBSBeautySpa.Mobile/       # .NET MAUI client + admin app
│       ├── Views/                 # XAML pages
│       ├── ViewModels/            # MVVM view models
│       ├── Models/                # Domain models (User, Booking, Service, Payment...)
│       ├── Services/              # Firebase, payment, notification service wrappers
│       └── Resources/             # Styles, colours, fonts, images
├── firebase/
│   ├── functions/                 # Cloud Functions (auth triggers, payment webhook, notifications)
│   └── firestore/                 # Firestore security rules & indexes
├── docs/
│   ├── proposal/                  # Deliverable 1 — Project Proposal
│   ├── diagrams/                  # ERD, Class, CRC, Use Case, Context, Activity, Sequence, DFD
│   └── wireframes/                # Figma exports / prototype screens
└── README.md
```

## Milestones

| Deliverable | Due date |
|---|---|
| Deliverable 1 — Project Proposal | 14 August 2026 |
| Deliverable 2 — Coding Design | 4 September 2026 |
| Deliverable 3 — Project Prototype | 16 October 2026 |
| Deliverable 4 — Final Presentation | 19–23 October 2026 |

## Getting started

> Prerequisites: [.NET 8 SDK](https://dotnet.microsoft.com/download) with the MAUI workload (`dotnet workload install maui`), a Firebase project, and Visual Studio 2022 or VS Code with the C# Dev Kit.

1. Clone the repo:
   ```
   git clone https://github.com/<your-org>/sbs-beauty-spa.git
   cd sbs-beauty-spa
   ```
2. Add your Firebase config files (these are gitignored — never commit them):
   - `src/SBSBeautySpa.Mobile/Platforms/Android/google-services.json`
   - `src/SBSBeautySpa.Mobile/Platforms/iOS/GoogleService-Info.plist`
3. Restore and run:
   ```
   cd src/SBSBeautySpa.Mobile
   dotnet restore
   dotnet build -t:Run -f net8.0-android
   ```
4. Deploy Cloud Functions:
   ```
   cd firebase/functions
   npm install
   firebase deploy --only functions
   ```

## Branching convention

- `main` — always deployable/demo-ready
- `dev` — integration branch
- `feature/<short-description>` — one branch per feature (e.g. `feature/booking-calendar`, `feature/payment-webhook`)

Open a PR into `dev`, get at least one teammate review, then merge. Merge `dev` into `main` at each milestone checkpoint.
