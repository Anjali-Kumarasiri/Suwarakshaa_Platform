# Member A (Leader) — Core Architecture, Domain, Auth, Requests & AI Governance

## Your Ownership
- **Core Domain & Database:** `backend/src/Domain/`, `backend/db/HumanitarianPlatform_Schema.sql`, `AppDbContext.cs`
- **Core API & Auth:** `Program.cs`, `AuthController.cs`, `RequestsController.cs`, `AiRecommendationsController.cs`, `DuplicatesController.cs`, `AuditController.cs`, `CrisesController.cs`, `HouseholdsController.cs`, `AttachmentsController.cs`
- **AI Governance & Outbox:** `AiGovernanceService.cs`, `OutboxProcessor.cs`, `GovernanceTests.cs`, `Workflow.cs`, `JwtTokenService.cs`, `SensitiveDataProtector.cs`
- **Project Root & CI/CD:** `README.md`, `CONTRIBUTING.md`, `docs/WORK-PLAN.md`, `.github/`

## Key Tasks (from WORK-PLAN.md)
1. Unit tests for `AiGovernanceService`: approve, modify, reject, defer; second approval for critical decisions; audit event written each time.
2. Email/phone verification with a one-time code (`Users.PhoneVerified`, `POST /api/auth/verify`, UI step after register).
3. Lockout and rate-limit tests for login (5 failures -> 15 min lock).
4. Architecture & AI Governance chapters in the final report.
