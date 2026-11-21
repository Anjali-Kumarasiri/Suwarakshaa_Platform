# Contributing

The Git history is the evidence of who did what, so please follow these rules.

## Branches and pull requests
- `main` is protected: no direct pushes. Turn on *Settings -> Branches -> Branch protection* (require a pull request and 1 approval).
- Work on a branch named `feature/<member>-<topic>`, e.g. `feature/m3-donation-form`; fixes use `fix/<member>-<topic>`.
- Open a Pull Request into `main`. **Someone else reviews it.** Fill in the PR template.
- Keep PRs small (one topic). Link the GitHub Issue (`Closes #12`).

## Commits
- Commit your own work, from your own account, with a clear message: `feat(donations): add flagged donation review`.
- Prefixes: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`.
- Do not commit: `bin/`, `obj/`, `node_modules/`, `.env`, real passwords or API keys (see `.gitignore`).

## Before you open a PR
```powershell
cd backend ; dotnet build ; dotnet test src\..\tests\Application.Tests
cd ..\client ; npm install ; npm run build
```
Then run the smoke test in `README.md`.

## Architecture rules (do not break)
- Controllers contain no business logic; rules live in `Domain` / `Application`.
- AI never changes a request by itself: only `AiGovernanceService.DecideAsync` applies decisions.
- Every AI-assisted decision writes an audit event; duplicates are flagged, never deleted.
- Every external call (AI, email, maps) has a timeout, retry and fallback.
- Critical state changes run inside `IUnitOfWork.ExecuteInTransactionAsync`.

## Academic honesty
Each member writes their own *Individual Contribution* section and lists their own commits/PRs in `docs/contribution-log.md`. If you used AI tools or other people's code, say so in the report as your module requires.
