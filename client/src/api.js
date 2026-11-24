const BASE = import.meta.env.VITE_API_URL ?? "http://localhost:5000";

export const auth = {
  get token() { return localStorage.getItem("token"); },
  get role() { return localStorage.getItem("role"); },
  get name() { return localStorage.getItem("name"); },
  save(token, role, name) { localStorage.setItem("token", token); localStorage.setItem("role", role ?? ""); localStorage.setItem("name", name ?? ""); },
  clear() { ["token", "role", "name"].forEach((k) => localStorage.removeItem(k)); },
};
export const http = {
  get: (p) => request(p), post: (p, b) => post(p, b),
  put: (p, b) => request(p, { method: "PUT", body: JSON.stringify(b) }),
  patch: (p, b) => request(p, { method: "PATCH", body: JSON.stringify(b) }), del: (p) => request(p, { method: "DELETE" }),
};
export async function upload(id, file) {
  const fd = new FormData(); fd.append("file", file);
  const res = await doFetch(`${BASE}/api/requests/${id}/attachments`, { method: "POST", headers: { Authorization: `Bearer ${auth.token}` }, body: fd });
  const d = await res.json().catch(() => null);
  if (!res.ok) throw new Error(d?.error || `Upload failed (${res.status})`);
  return d;
}
export async function download(id, aid) {
  const res = await doFetch(`${BASE}/api/requests/${id}/attachments/${aid}/download`, { headers: { Authorization: `Bearer ${auth.token}` } });
  if (!res.ok) throw new Error("Download failed");
  window.open(URL.createObjectURL(await res.blob()), "_blank");
}
export const STAFF = ["EmergencyCoordinator", "CaseManager"];
export const CONSOLE_ROLES = ["EmergencyCoordinator", "CaseManager", "LogisticsManager", "ShelterAdmin", "VolunteerCoordinator", "DonorOfficer", "SystemAdmin"];

const doFetch = (u, o) => fetch(u, o).catch(() => { throw new Error(`Cannot reach the server at ${BASE}. Start the backend and try again.`); });

async function request(path, options = {}) {
  const res = await doFetch(BASE + path, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(auth.token ? { Authorization: `Bearer ${auth.token}` } : {}),
      ...options.headers,
    },
  });
  const data = await res.json().catch(() => null);
  if (!res.ok) {
    // backend sends { error } or ASP.NET validation { errors: { Field: [msgs] } }
    const first = data?.errors ? Object.values(data.errors).flat()[0] : null;
    throw new Error(data?.error || first || data?.title || `Request failed (${res.status})`);
  }
  return data;
}
const post = (p, body) => request(p, { method: "POST", body: JSON.stringify(body) });

export const api = {
  login: (identifier, password) => post("/api/auth/login", { identifier, email: identifier, password }),   // identifier = email or phone            // -> { token, role, fullName }
  register: (d) => post("/api/auth/register", d),                                        // { email, password, fullName, phone }
  submitRequest: (d) => post("/api/requests", d),                                        // Citizen only
  myRequests: () => request("/api/requests/mine"),
  pendingRecommendations: () => request("/api/ai-recommendations?status=PendingReview"),
  decide: (id, action, finalValueJson, reason) => post(`/api/ai-recommendations/${id}/decision`, { action, finalValueJson, reason }),
  pendingApproval: () => request("/api/ai-recommendations/pending-approval"),            // EmergencyCoordinator only
  secondApproval: (id, approve, reason) => post(`/api/ai-recommendations/${id}/second-approval`, { approve, reason }),
  publicShelters: () => request("/api/shelters/public"),
  publicAlerts: () => request("/api/alerts/public"),
  ask: (message, language) => post("/api/assistant/ask", { message, language }),   // public chatbot
  metrics: () => request("/api/dashboard/metrics"),
  cases: (filter) => request(`/api/dashboard/cases?filter=${filter}`),
  users: () => request("/api/admin/users?pageSize=100"),
  setActive: (id, isActive) => post(`/api/admin/users/${id}/active`, { isActive }),
  audit: () => request("/api/audit?pageSize=100"),
  notifications: () => request("/api/notifications/mine"),
  campaigns: () => request("/api/donations/campaigns"),
  transparency: (id) => request(`/api/donations/transparency/${id}`),
  donate: (d) => post("/api/donations", d),     // { campaignId, type:"Monetary", amount, currency, anonymous }
  myDonations: () => request("/api/donations/mine"),
};
