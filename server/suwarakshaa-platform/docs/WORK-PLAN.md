# Work plan: who does what

The platform already runs end to end. What is left is real work that five people can share, and each person can only show a contribution they actually made. So every task below is something a member does **from their own GitHub account**: code, tests, documentation, review.

Pick a letter, put your name next to it, and open the matching GitHub Issues (copy the task lines). Nobody has to be "the leader"; whoever creates the repository only makes the first commit (`chore: initial project import`).

| Letter | Member (fill in) | Part |
|---|---|---|
| A |  | Core workflow and AI governance (the heaviest and most central part) |
| B |  | Resources and shelters |
| C |  | Volunteers, donations and admin |
| D |  | AI service, chatbot and notifications |
| E |  | Frontend polish, testing, deployment |

## A: Core workflow and AI governance
Owns: `AuthController`, `RequestsController`, `AiGovernanceService`, `OutboxProcessor`, `db/`.
- [ ] Unit tests for `AiGovernanceService`: approve, modify, reject, defer; second approval for critical decisions; audit event written each time.
- [ ] Email/phone verification with a one-time code (use the SMS/email stub; add `Users.PhoneVerified`, endpoint `POST /api/auth/verify`, UI step after register).
- [ ] Lockout and rate-limit tests for login (5 failures -> 15 min lock).
- [ ] Report: architecture chapter (layers, modular monolith, outbox pattern) and AI governance chapter.

## B: Resources and shelters
Owns: `ResourcesController`, `SheltersController`, allocation strategies, console pages *Shelters* and *Resources*.
- [ ] Unit tests for the allocation strategies (shortest distance vs highest capacity) and "never over capacity".
- [ ] Replace the prompt dialogs on *Shelters* / *Resources* (`client/src/pages/Staff.jsx`) with proper forms (allocate, restock, check out).
- [ ] Show shelter facilities and free spaces on the public map popup.
- [ ] Report: Strategy pattern and resource allocation chapter.

## C: Volunteers, donations and admin
Owns: `VolunteersController`, `DonationsController`, `AdminController`, pages *Donate*, *Volunteer*, *Users*.
- [ ] **Material donations**: public endpoint for resource types, then a "Material" option on the Donate form (backend + frontend).
- [ ] Unit tests: idempotent payment reference, fraud flag rules, volunteer ranking.
- [ ] Replace the prompt dialogs on *Volunteers* / *Donations* with forms (verify volunteer, record spending).
- [ ] Report: donations, transparency and admin chapter.

## D: AI service, chatbot and notifications
Owns: `AiService`, `LocalFaq`, `NotificationDispatcher`, `Notifier.jsx`, `ChatBot.jsx`.
- [ ] **Chatbot memory**: send the last few messages (backend `AssistantController` + `AiService /assist` + `ChatBot.jsx`), keep the 500-character and prompt-injection limits.
- [ ] Unit tests: keyword classification, LLM output validation (category/urgency clamped), notification retry and dead-letter.
- [ ] Have a Sinhala and a Tamil speaker review the templates and FAQ answers; fix wording.
- [ ] Document SMTP / `ANTHROPIC_API_KEY` setup. Report: AI services, resilience (Polly retry, circuit breaker, fallback) chapter.

## E: Frontend polish, testing, deployment
Owns: `client/`, test assets, deployment.
- [ ] Analytics page: real charts (KPI history, workload, forecast) instead of tables.
- [ ] Accessibility and mobile pass on Home, Login, Donate, Map, console.
- [ ] Postman collection (login, request lifecycle, decisions) and a written UAT script covering the README smoke test.
- [ ] Deploy (see `README.md`: Azure SQL + App Service + Netlify/Vercel) and record the live URL.
- [ ] Upgrade the backend from .NET 8 to .NET 10 in a branch (`TargetFramework`, package versions) and fix any build errors.

## Everyone
- [ ] Review at least two pull requests from other members before they are merged.
- [ ] Add your section to `docs/contribution-log.md` (your commits, PRs, tests, what you learned).
- [ ] Be able to explain your part in the demonstration: how it works and why it was designed that way. Several parts of this code base were produced with AI assistance, so read, run and understand what you own, and declare AI use as your module requires.

## Workflow (short)
1. One person creates the repo, pushes the first commit, adds the other four as collaborators and protects `main`.
2. Everyone clones, creates `feature/<letter>-<topic>`, commits their own work, opens a PR, gets a review, merges.
3. Never share one account or commit on behalf of someone else. Each person's history must be their own.
