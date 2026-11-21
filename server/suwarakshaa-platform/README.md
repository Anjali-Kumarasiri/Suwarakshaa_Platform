# Suwarakshaa (සුවරක්ෂා)

AI-assisted humanitarian crisis management platform (SE205.3 Software Architecture, group coursework).
Citizens ask for help and donate, coordinators review AI suggestions, and **a person always makes the final decision**.

| Part | Folder | Tech |
|---|---|---|
| Web app (citizen portal + staff console) | `client/` | React 18, Vite, React Router, Leaflet |
| Core API (modular monolith, Clean Architecture) | `backend/src/Api` + `Application` + `Domain` + `Infrastructure` | ASP.NET Core Web API (net8.0), EF Core, JWT |
| AI service (optional) | `backend/src/AiService` | .NET minimal API, keyword rules, optional LLM |
| Notification service (optional) | `backend/src/NotificationService` | .NET worker |
| Database | `backend/db/HumanitarianPlatform_Schema.sql` | SQL Server (LocalDB or Docker) |

```
Browser (React, :5173)  ->  Core API (:5000)  ->  SQL Server
                                  |
                                  +-> AI service (:5100, optional: fallback rules/FAQ are built in)
```

## 1. Prerequisites
- Node.js LTS (https://nodejs.org)
- .NET SDK 8 or newer. .NET 8 reached end of support on 10 Nov 2026; a newer SDK/runtime works if you set `DOTNET_ROLL_FORWARD=Major` (shown below).
- SQL Server: **LocalDB** (comes with Visual Studio) or **Docker**
- Git

## 2. Database (once)
**LocalDB (default, matches `appsettings.json`):**
```powershell
sqllocaldb start MSSQLLocalDB
sqlcmd -S "(localdb)\MSSQLLocalDB" -I -i backend\db\HumanitarianPlatform_Schema.sql
```
`-I` is required (quoted identifiers). Run the script **once**; it creates the `HumanitarianPlatform` database and seeds roles.

**Docker instead:**
```powershell
cd backend
docker compose up sqlserver -d
$env:ConnectionStrings__Default="Server=localhost,1433;Database=HumanitarianPlatform;User Id=sa;Password=Your_strong_Passw0rd;TrustServerCertificate=True"
```
Then run the schema script against `localhost,1433` (SSMS, Azure Data Studio or `sqlcmd -U sa`).

Existing database from before phone login? Run `backend\db\phone_login.sql` once.

## 3. Run the backend
```powershell
cd backend
dotnet build
cd src\Api
$env:DOTNET_ROLL_FORWARD="Major"
$env:ASPNETCORE_ENVIRONMENT="Development"     # Development seeds the demo users
dotnet .\bin\Debug\net8.0\Api.dll --urls http://localhost:5000
```
Check: http://localhost:5000/health -> `ok`, http://localhost:5000/swagger.
Stop it with `Ctrl+C` before running `dotnet build` again (otherwise "file is locked").

Optional AI service (chatbot also works without it):
```powershell
cd backend\src\AiService
$env:DOTNET_ROLL_FORWARD="Major"
dotnet .\bin\Debug\net8.0\AiService.dll --urls http://localhost:5100
```
Set `ANTHROPIC_API_KEY` first if you want real LLM answers; without it keyword rules are used.

## 4. Run the frontend
```powershell
cd client
npm install
copy .env.example .env        # VITE_API_URL=http://localhost:5000
npm run dev                   # http://localhost:5173
```

## 5. Demo accounts (Development only)
| Role | Email | Password |
|---|---|---|
| SystemAdmin | admin@relief.test | Admin@12345 |
| EmergencyCoordinator | coordinator@relief.test | Coord@12345 |
| CaseManager | casemanager@relief.test | Case@12345 |
| Citizen | citizen@relief.test | Citizen@12345 |
| LogisticsManager | logistics@relief.test | Logistics@123 |
| ShelterAdmin | shelter@relief.test | Shelter@1234 |
| VolunteerCoordinator | volunteers@relief.test | Volunteer@123 |
| DonorOfficer | donations@relief.test | Donation@123 |

Citizens can register with an **email, a phone number, or both** and log in with either. Staff go to `/console`.

## 6. Smoke test (run this after every merge to `main`)
1. `/health` is `ok`, Swagger opens, `dotnet build` has 0 errors, `dotnet test` passes.
2. Citizen: register (phone only), log in, submit a request, open it from *My requests*, upload a file.
3. Citizen: donate, check the pop-up and the bell notification; open the shelters map; ask the chatbot (EN/SI/TA).
4. Coordinator: *Review queue* -> approve / modify / reject / defer; *All requests*; publish an alert.
5. Admin: dashboard cards open their lists; *Users* -> create a staff account and assign a role; *Audit log*.

## 7. Team and ownership
The five parts (A to E), the remaining tasks for each, and the Git workflow are in [`docs/WORK-PLAN.md`](docs/WORK-PLAN.md). Fill in the names there. Each member documents their own part in `docs/contribution-log.md`.

| Letter | Member | Part |
|---|---|---|
| A | | Core workflow and AI governance |
| B | | Resources and shelters |
| C | | Volunteers, donations, admin |
| D | | AI service, chatbot, notifications |
| E | | Frontend polish, testing, deployment |

## 8. Known limits (state these honestly in the report)
- No antivirus scan on uploads; payment and SMS are sandbox/stubs; no refresh tokens or push notifications.
- Citizens cannot yet donate **material** items (needs a public resource-types endpoint).
- Chatbot is single-turn (no conversation memory). Analytics shows tables and cards, not charts.
- Phone numbers are not verified (no OTP); email format is validated but not confirmed.
- Phone-only accounts store an internal placeholder email (`...@phone.invalid`) because `Users.Email` is `NOT NULL`.
- Staff console forms for some actions use simple prompts instead of full forms.
- Change the JWT key, seeded passwords and connection strings before any real deployment.

## 9. Troubleshooting
| Problem | Fix |
|---|---|
| `Cannot reach the server` in the browser | Backend is not running, or `VITE_API_URL` is wrong. Open `/health`. |
| `address already in use` on 5000 | `Get-Process dotnet \| Stop-Process -Force`, then start again |
| `Application Control policy has blocked this file` | Move the repo out of `Downloads` (e.g. `C:\Projects`) and run `Get-ChildItem -Recurse . \| Unblock-File` |
| `You must install .NET 8 runtime` | Keep `DOTNET_ROLL_FORWARD=Major` set |
| Login says invalid, demo users missing | Start the API with `ASPNETCORE_ENVIRONMENT=Development` after the schema script ran |
| CORS error in the console | Add your frontend URL to `Cors:Origins` in `backend/src/Api/appsettings.json` |

See `CONTRIBUTING.md` for the Git workflow.
