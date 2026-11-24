# Relief portal (React client)

```bash
npm install
cp .env.example .env     # set VITE_API_URL to the Core API
npm run dev              # http://localhost:5173
```
Pages: /login, /request (citizen), /queue (coordinator: approve / modify / reject / defer AI suggestions).
Add CORS for http://localhost:5173 in the Core API (`builder.Services.AddCors(...)`) before connecting.
Member 5: add shelters, resources, volunteers, admin pages and a map component (Leaflet/Google Maps).
