import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { http } from "../api";
import { toast } from "../toast";
import { AutoView, Table, Note, head, fmt, useLoad } from "./Manage";

const pk = (r) => r[Object.keys(r).find((k) => /id$/i.test(k))];
const items = (d) => (Array.isArray(d) ? d : d?.items ?? []);
const ask = (q, def) => window.prompt(q, def ?? "");

// Generic list + create form + row actions. Each action is [label, fn(row)]: fn returns a promise, a path string (navigate) or nothing.
function Panel({ title, path, create, actions = [], hide = [], note }) {
  const nav = useNavigate();
  const [d, err, load] = useLoad(path);
  const [f, setF] = useState(() => Object.fromEntries((create?.fields || []).map((x) => [x.k, x.def ?? (x.type === "checkbox" ? false : "")])));
  const [msg, setMsg] = useState("");
  const rows = items(d);
  const cols = rows[0] ? Object.keys(rows[0]).filter((k) => !hide.includes(k) && (rows[0][k] === null || typeof rows[0][k] !== "object")) : [];
  const run = (p) => p.then(() => { setMsg(""); toast("Saved."); load(); }).catch((e) => setMsg(e.message));
  const body = () => Object.fromEntries(create.fields.map((x) => [x.k, x.type === "number" ? Number(f[x.k]) : f[x.k] === "" ? null : f[x.k]]));
  const click = (a, r) => { const out = a[1](r); if (typeof out === "string") nav(out); else if (out) run(out); };
  return (<>
    {title && <h2>{title}</h2>}{note && <p className="muted">{note}</p>}<Note err={err || msg} />
    {create && <form className="card" onSubmit={(e) => { e.preventDefault(); run(http.post(create.url, body())); }}>
      <h3>{create.title}</h3>
      <div className="grid-2">{create.fields.map((x) => <div key={x.k}><label>{x.label}</label>
        {x.options ? <select value={f[x.k]} onChange={(e) => setF({ ...f, [x.k]: e.target.value })} required><option value="">Choose...</option>{x.options.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select>
          : x.type === "checkbox" ? <input type="checkbox" checked={f[x.k]} onChange={(e) => setF({ ...f, [x.k]: e.target.checked })} />
          : <input type={x.type || "text"} step="any" value={f[x.k]} onChange={(e) => setF({ ...f, [x.k]: e.target.value })} required={!x.optional} />}</div>)}</div>
      <button className="primary">{create.button || "Add"}</button></form>}
    {d && !rows.length ? <div className="card">Nothing here yet.</div> : rows.length > 0 && <div className="tbl"><table>
      <thead><tr>{cols.map((c) => <th key={c}>{head(c)}</th>)}{actions.length > 0 && <th />}</tr></thead>
      <tbody>{rows.slice(0, 100).map((r, i) => <tr key={i}>{cols.map((c) => <td key={c}>{fmt(r[c])}</td>)}
        {actions.length > 0 && <td className="acts">{actions.map((a) => <button key={a[0]} onClick={() => click(a, r)}>{a[0]}</button>)}</td>}</tr>)}</tbody></table></div>}
  </>);
}
function Tabbed({ title, tabs }) {
  const [t, setT] = useState(0);
  return <><h2>{title}</h2><div className="chips">{tabs.map(([n], i) => <button key={n} className={i === t ? "on" : ""} onClick={() => setT(i)}>{n}</button>)}</div><div key={t}>{tabs[t][1]}</div></>;
}
const status = (url, q) => (r) => { const s = ask(q, r.status); return s && http.patch(url(r), { status: s }); };

export const Requests = () => <Panel title="All requests" path="/api/requests?pageSize=100" hide={["description", "latitude", "longitude", "formattedAddress", "updatedAt", "assignedTo"]}
  note="Open a request to see its timeline. Statuses: Submitted, UnderReview, Verified, Escalated, Assigned, InProgress, Fulfilled, Rejected, Closed."
  actions={[["Open", (r) => `/console/requests/${r.requestId}`],
    ["Status", status((r) => `/api/requests/${r.requestId}/status`, "New status")],
    ["Assign", (r) => { const o = ask("Organisation ID (see Organisations page)"); return o && http.patch(`/api/requests/${r.requestId}/assign`, { organisationId: Number(o) }); }]]} />;

export function RequestView() {
  const { id } = useParams(); const [d, err] = useLoad(`/api/requests/${id}`);
  return <><Link className="back" to="/console/requests">← All requests</Link><h2>{d?.referenceNo ?? "Request"}</h2><Note err={err} />
    {d && <><div className="card"><span className="pill">{fmt(d.status)}</span> {d.category} · {d.peopleAffected} people <p>{d.description}</p></div>
      {Object.entries(d).filter(([, v]) => Array.isArray(v)).map(([k, v]) => <div key={k}><h3>{head(k)}</h3><Table rows={v} /></div>)}</>}</>;
}

export const Shelters = () => <Panel title="Shelters" path="/api/shelters" note="Allocation never goes over capacity. Allocate a family by request ID."
  create={{ title: "Register shelter", url: "/api/shelters", fields: [{ k: "name", label: "Name" }, { k: "address", label: "Address", optional: true }, { k: "latitude", label: "Latitude", type: "number" }, { k: "longitude", label: "Longitude", type: "number" }, { k: "capacity", label: "Capacity", type: "number", def: 50 }, { k: "isAccessible", label: "Wheelchair accessible", type: "checkbox" }] }}
  actions={[["Allocate", (r) => { const q = ask("Request ID?"); const n = q && ask("People count?", "1"); return n && http.post(`/api/shelters/${pk(r)}/allocations`, { requestId: Number(q), peopleCount: Number(n) }); }],
    ["Facility", (r) => { const x = ask("Facility (e.g. Water, Toilets, Medical)"); return x && http.post(`/api/shelters/${pk(r)}/facilities`, { facility: x }); }],
    ["Check out", () => { const a = ask("Allocation ID to check out?"); return a && http.post(`/api/shelters/allocations/${a}/checkout`, {}); }]]} />;

export function Resources() {
  const [types] = useLoad("/api/resources/types"); const opts = (types || []).map((t) => [pk(t), t.name]);
  return <Tabbed title="Resources" tabs={[
    ["Inventory", <Panel path="/api/resources/inventory" create={{ title: "Add stock", url: "/api/resources/inventory", fields: [{ k: "resourceTypeId", label: "Type", type: "number", options: opts }, { k: "locationName", label: "Location", optional: true }, { k: "quantity", label: "Quantity", type: "number" }] }}
      actions={[["Restock", (r) => { const q = ask("Quantity to add?"); return q && http.post(`/api/resources/inventory/${pk(r)}/restock`, { quantity: Number(q) }); }],
        ["Allocate", (r) => { const q = ask("Request ID?"); const n = q && ask("Quantity?"); return n && http.post("/api/resources/allocate", { inventoryId: pk(r), requestId: Number(q), quantity: Number(n) }); }]]} />],
    ["Allocations", <Panel path="/api/resources/allocations" actions={[["Status", status((r) => `/api/resources/allocations/${pk(r)}/status`, "New status")], ["Dispatch", (r) => http.post(`/api/resources/allocations/${pk(r)}/dispatch`, {})]]} />],
    ["Partner requests", <Panel path="/api/resources/partner-requests" actions={[["Status", status((r) => `/api/resources/partner-requests/${pk(r)}/status`, "New status")]]} />],
  ]} />;
}

export const Volunteers = () => <Tabbed title="Volunteers" tabs={[
  ["Volunteers", <Panel path="/api/volunteers" actions={[["Verify", (r) => { const s = ask("Status (Verified or Rejected)", "Verified"); return s && http.post(`/api/volunteers/${pk(r)}/verification`, { status: s }); }]]} />],
  ["Tasks", <Panel path="/api/volunteers/tasks" create={{ title: "New task", url: "/api/volunteers/tasks", fields: [{ k: "title", label: "Title" }, { k: "description", label: "Description", optional: true }, { k: "volunteersNeeded", label: "Volunteers needed", type: "number", def: 1 }, { k: "requestId", label: "Request ID (optional)", type: "number", optional: true }, { k: "startsAt", label: "Starts", type: "datetime-local", optional: true }] }}
    actions={[["Matches", (r) => http.get(`/api/volunteers/tasks/${pk(r)}/matches`).then((m) => window.alert(items(m).slice(0, 8).map((x) => `ID ${pk(x)}  ${x.fullName ?? x.name ?? ""}  score ${x.score ?? ""}`).join("\n") || "No matches"))],
      ["Assign", (r) => { const v = ask("Volunteer ID to assign?"); return v && http.post(`/api/volunteers/tasks/${pk(r)}/assign`, { volunteerId: Number(v) }); }]]} />],
]} />;

export const Donations = () => <Tabbed title="Donations" tabs={[
  ["Campaigns", <Panel path="/api/donations/campaigns" create={{ title: "New campaign", url: "/api/donations/campaigns", fields: [{ k: "name", label: "Name" }, { k: "targetAmount", label: "Target amount", type: "number" }, { k: "endsOn", label: "Ends on", type: "date", optional: true }, { k: "organisationId", label: "Organisation ID", type: "number", def: 1 }] }}
    actions={[["Record spending", (r) => { const p = ask("What was it spent on?"); const a = p && ask("Amount?"); return a && http.post(`/api/donations/campaigns/${pk(r)}/spend`, { purpose: p, amount: Number(a) }); }]]} />],
  ["Flagged donations", <Panel path="/api/donations/flagged" note="Flagged for review, never rejected automatically." actions={[["Clear flag", (r) => http.post(`/api/donations/${pk(r)}/review`, { clear: true })], ["Keep flagged", (r) => http.post(`/api/donations/${pk(r)}/review`, { clear: false })]]} />],
  ["Flagged allocations", <Panel path="/api/donations/flagged-allocations" />],
]} />;

export const Alerts = () => <Panel title="Emergency alerts" path="/api/alerts/public" note="Shown on every public page. Tick Notify users to also send it as an announcement."
  create={{ title: "Publish alert", url: "/api/alerts", button: "Publish", fields: [{ k: "title", label: "Title" }, { k: "body", label: "Message" }, { k: "severity", label: "Severity", options: [["Info", "Info"], ["Warning", "Warning"], ["Critical", "Critical"]], def: "Info" }, { k: "language", label: "Language", options: [["en", "English"], ["si", "Sinhala"], ["ta", "Tamil"]], def: "en" }, { k: "notifyUsers", label: "Notify users", type: "checkbox" }] }}
  actions={[["Deactivate", (r) => http.patch(`/api/alerts/${pk(r)}/deactivate`, {})]]} />;

export function Review() {
  const [d, err] = useLoad("/api/ai-recommendations/review");
  return <><h2>AI review</h2><p className="muted">How AI suggestions compared with human decisions.</p><Note err={err} />{d && <AutoView data={d} />}</>;
}
