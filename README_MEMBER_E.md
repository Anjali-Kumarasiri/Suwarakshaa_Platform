# Member E — Frontend Core, UI/UX, Citizen Portal & Deployment

## Your Ownership
- **Frontend Core & Routing:** `client/src/App.jsx`, `client/src/main.jsx`, `client/src/api.js`, `client/src/validate.js`, `client/src/toast.js`, `client/src/styles.css`, `client/src/home.css`
- **Public & Citizen Pages:** `Home.jsx`, `Login.jsx`, `Citizen.jsx`, `CitizenRequest.jsx`, `Updates.jsx`, `updates.js`, `Brand.jsx`, `Photo.jsx`
- **Assets & Deployment Config:** `client/public/`, `client/package.json`, `client/vite.config.js`, `client/vercel.json`, `backend/docker-compose.yml`, `backend/Dockerfile.template`, `backend/docs/api-tests.http`

## Key Tasks (from WORK-PLAN.md)
1. Analytics page: real charts (KPI history, workload, forecast) instead of tables.
2. Accessibility and mobile responsiveness pass on Home, Login, Donate, Map, and Console.
3. Postman collection (login, request lifecycle, decisions) and UAT script covering the README smoke test.
4. Deploy (Azure SQL + App Service + Netlify/Vercel) and upgrade backend from .NET 8 to .NET 10 in a branch.
