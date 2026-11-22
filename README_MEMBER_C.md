# Member C — Volunteers, Donations, Admin & Analytics

## Your Ownership
- **Backend Controllers:** `VolunteersController.cs`, `DonationsController.cs`, `AdminController.cs`, `OrganisationsController.cs`, `AnalyticsController.cs`, `DashboardController.cs`, `AnomaliesController.cs`, `FeedbackController.cs`
- **Services & Strategies:** `Ranking.cs`, `Analytics.cs`, `KpiSnapshotService.cs`, `MetricsService.cs`
- **Frontend Pages:** `client/src/pages/Donate.jsx`, `client/src/pages/Manage.jsx`, `client/src/pages/Console.jsx`

## Key Tasks (from WORK-PLAN.md)
1. Material donations: public endpoint for resource types + "Material" option on the Donate form (backend + frontend).
2. Unit tests: idempotent payment reference, fraud flag rules, volunteer ranking.
3. Replace prompt dialogs on Volunteers / Donations with forms (verify volunteer, record spending).
4. Report: donations, transparency and admin chapter.
