# SE205.3 - AI-Powered Humanitarian Crisis Platform (Starter)

Architecture: **Hybrid** = Core API (modular monolith, Clean Architecture) + AI Service + Notification Service,
SQL Server, RabbitMQ (async events via transactional outbox), React client.

> NOTE: this skeleton was written without compiling. First run `dotnet build`, fix any small errors, then commit.

## 1. One-time setup (Member 1)
```bash
dotnet new sln -n HumanitarianPlatform
dotnet sln add src/Domain src/Application src/Infrastructure src/Api src/AiService src/NotificationService tests/Application.Tests
dotnet build
```
Run `db/HumanitarianPlatform_Schema.sql` in SQL Server (SSMS), or `docker compose up sqlserver` first.

## 2. Run
```bash
docker compose up --build        # sql server, rabbitmq, ai-service, core-api, notification-service
# Swagger: http://localhost:5000/swagger
```

## 3. Layers (dependencies point inwards)
`Api -> Infrastructure -> Application -> Domain`
- Domain: entities + business rules (no packages)
- Application: interfaces, services, strategies (Strategy pattern)
- Infrastructure: EF Core repositories, AI client (Polly), email/map clients
- Api: controllers, JWT, Swagger, rate limiting

## 4. Team split
| Member | Owns | Main deliverables |
|---|---|---|
| 1 (Lead) | Auth/RBAC, Requests, AI governance, DB, Outbox dispatcher | AuthController, RequestsController, AiGovernanceService, audit |
| 2 | Resources + Shelters | MultiConstraintAllocationStrategy, ShortageResolver, shelter over-allocation checks |
| 3 | Volunteers + Donations + Admin | volunteer matching, tasks, donations, user/role admin |
| 4 | AI Service + Notification Service | /classify /similarity /anomaly, RabbitMQ consumer, email, idempotency, retries |
| 5 | React client + testing | citizen portal, coordinator dashboard, Postman collection, UAT script |

Everyone writes **unit tests for their own module** and their own **Individual Contribution** section.

## 5. Git rules
- `main` is protected. Work in `feature/<member>-<topic>` branches, open a Pull Request, someone else reviews.
- Small commits with clear messages. Use GitHub Issues per task.
- This is the evidence for the 10% individual contribution mark.

## 6. Architecture rules (do not break)
- Controllers contain no business logic. Rules live in Domain/Application.
- AI never changes a request by itself: only `AiGovernanceService.DecideAsync` applies decisions.
- Every AI-assisted decision writes an AuditEvent. Duplicate requests are flagged, never deleted.
- Every external call (AI, email, maps) has timeout + retry + fallback.
- Critical state changes happen inside `IUnitOfWork.ExecuteInTransactionAsync`.

## 7. Backend status: all modules implemented
Swagger: `/swagger`. Ready-made calls: `docs/api-tests.http` (VS Code REST Client). Run `db/HumanitarianPlatform_Schema.sql` first.

| Brief section | What is implemented (endpoints under `/api/...`) |
|---|---|
| A. Citizen portal | `auth` register/login, `households/me` (encrypted profile), `requests` submit/mine/track, `requests/{id}/attachments` (secure upload), `requests/{id}/feedback`, `alerts/public`, `alerts/guidance`, `shelters/public`, `assistant/ask` (multilingual AI assistant), `notifications/mine` |
| B. Coordination | `crises` (create, close with unresolved-work check), `crises/{id}/areas`, `crises/{id}/organisations`, `organisations`, `requests` list/status/assign/monitor (overdue, escalated), `duplicates` (merge or dismiss), `anomalies`, request timeline in `requests/{id}`, `analytics/summary` (AI briefing), `dashboard/metrics` |
| C. AI support | classification + urgency (`ai-service` /classify), duplicates, anomaly detection (rules, explained), resource and shelter matching (`resources/suggest`, `shelters/suggest`), volunteer matching (`volunteers/tasks/{id}/matches`), forecasting (`analytics/forecast`), multilingual templates (EN/SI/TA) + AI translation/assistant, crisis summary |
| D. Governance | approve / modify / reject / defer, original AI output + confidence kept, audit record per decision, **second approval (four-eyes) for critical decisions**, `ai-recommendations/review` (post-incident review) |
| E. Resources | inventory, restock, atomic allocate, `resolve-shortage` (priority then oldest), alternatives, partner requests, **dispatch with route/ETA**, delivery status |
| F. Shelters | register, facilities, accessibility, service radius, atomic allocation (never over capacity), checkout, resource requests to partners |
| G. Volunteers | register + skills, verification, availability, tasks, ranked matches, assign/accept/complete, hours |
| H. Donations | donor profile, campaigns, monetary + material donations, idempotent payment ref, fraud flags, **allocation flagging**, spending tracking, public transparency |
| I. Communication | in-app, email (SMTP or log), SMS (provider stub + phone number), emergency announcements (`alerts` with notifyUsers), retries + dead letters, duplicate-safe, EN/SI/TA |
| J. Maps | geocode, reverse geocode, route distance/time (OSRM, estimate if down), operational areas, `analytics/coverage` |
| Admin / audit / KPIs | users, roles, staff accounts, organisations, read-only audit log, hourly KPI snapshots (`analytics/kpis`) |

Async flow: submit -> outbox event (same DB transaction) -> `OutboxProcessor` -> AI suggestion (fallback if AI is down) + duplicate check + anomaly check
-> coordinator decides (critical = second approval) -> applied + audit -> notifications via `NotificationDispatcher` (retry / backoff / dead letter).

Seeded demo users (Development, after the SQL script has been run):
| Email | Password | Role |
|---|---|---|
| citizen@relief.test | Citizen@12345 | Citizen |
| coordinator@relief.test | Coord@12345 | EmergencyCoordinator |
| casemanager@relief.test | Case@12345 | CaseManager |
| logistics@relief.test | Logistics@123 | LogisticsManager |
| shelter@relief.test | Shelter@1234 | ShelterAdmin |
| volunteers@relief.test | Volunteer@123 | VolunteerCoordinator |
| donations@relief.test | Donation@123 | DonorOfficer |
| admin@relief.test | Admin@12345 | SystemAdmin |

Optional settings: `ANTHROPIC_API_KEY` (real AI, otherwise keyword rules / canned answers), `Smtp:*` in appsettings (real email, otherwise logged),
`Security:RequireHttps=true` + a certificate for production (HSTS + redirect are already wired).

## 8. Security summary (for the report)
JWT + RBAC, BCrypt hashing, lockout, per-IP rate limits (default / auth / assistant), input validation (data annotations) + `TextSanitizer`,
secure file upload (content sniffing, size/count limits, random names, nosniff), encryption of medical notes and addresses (Data Protection),
audit of sensitive reads, security headers, generic 500 responses with a trace id, four-eyes approval for critical AI decisions.

## 9. Known limits (write these in the report)
- No antivirus scan on uploads, no real payment gateway or SMS provider (stubs), no refresh tokens, no push notifications (channel stub only).
- Forecasting is a linear-trend baseline (confidence is reported), duplicate detection is a word-overlap heuristic, anomaly detection is rule-based.
- Sinhala / Tamil templates should be reviewed by a native speaker. Geocoding (Nominatim) and routing (OSRM demo server) have usage limits and no SLA.
- Notifications run in-process (`NotificationDispatcher`); RabbitMQ + a separate Notification Service are an optional extension.
- Several controllers use `AppDbContext` directly for speed; business rules live in Domain/Application (status flow, governance, shortage, ranking, forecasting, anomaly).
- New requests/shelters/stock attach to the first ACTIVE crisis (requests may pass `crisisId`).
- Change the JWT key, seeded passwords and connection strings before any real deployment.
