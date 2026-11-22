import { useEffect, useState } from "react";
import { http, auth } from "../api";
import { toast } from "../toast";

export const head = (k) => k.replace(/([A-Z])/g, " $1").replace(/^./, (c) => c.toUpperCase());
export const fmt = (v) => v === null || v === undefined ? "-" : Array.isArray(v) ? v.join(", ") : typeof v === "object" ? JSON.stringify(v)
  : typeof v === "string" && /^\d{4}-\d\d-\d\dT/.test(v) ? new Date(v).toLocaleString() : String(v);
const rn = (r) => (typeof r === "string" ? r : r.name ?? r.Name);

export function useLoad(path) {
  const [d, setD] = useState(null); const [err, setErr] = useState("");
  const load = () => http.get(path).then((x) => { setD(x); setErr(""); }).catch((e) => setErr(e.message));
  useEffect(() => { setD(null); load(); }, [path]);
  return [d, err, load];
}
export const Note = ({ err }) => (err ? <p className="error">{err}</p> : null);

export function Table({ rows }) {
  if (!rows?.length) return <div className="card">Nothing here yet.</div>;
  const cols = Object.keys(rows[0]);
  return <div className="tbl"><table><thead><tr>{cols.map((c) => <th key={c}>{head(c)}</th>)}</tr></thead>
    <tbody>{rows.slice(0, 100).map((r, i) => <tr key={i}>{cols.map((c) => <td key={c}>{fmt(r[c])}</td>)}</tr>)}</tbody></table></div>;
}
export function AutoView({ data }) {
  if (Array.isArray(data)) return <Table rows={data} />;
  if (!data || typeof data !== "object") return <p>{fmt(data)}</p>;
  const ent = Object.entries(data), isObj = (v) => v && typeof v === "object";
  return <>
    <div className="stat-grid">{ent.filter(([, v]) => !isObj(v)).map(([k, v]) => <div className="stat" key={k}><strong>{fmt(v)}</strong><span>{head(k)}</span></div>)}</div>
    {ent.filter(([, v]) => isObj(v)).map(([k, v]) => <div key={k}><h3>{head(k)}</h3><AutoView data={v} /></div>)}
  </>;
}

export function Analytics() {
  const tabs = [["workload", "Workload"], ["shortages", "Shortages"], ["forecast", "Forecast"], ["coverage", "Coverage"], ["feedback", "Feedback"], ["kpis", "KPI history"]];
  const [tab, setTab] = useState("workload");
  const [d, err] = useLoad(`/api/analytics/${tab}`);
  return <><h2>Analytics</h2>
    <div className="chips">{tabs.map(([k, n]) => <button key={k} className={tab === k ? "on" : ""} onClick={() => setTab(k)}>{n}</button>)}</div>
    <Note err={err} />{!d && !err && <p className="muted">Loading...</p>}{d && <AutoView data={d} />}</>;
}

export function Users() {
  const [rows, err, load] = useLoad("/api/admin/users?pageSize=100");
  const [roles] = useLoad("/api/admin/roles");
  const [f, setF] = useState({ fullName: "", email: "", password: "", role: "" });
  const [pick, setPick] = useState({}); const [msg, setMsg] = useState("");
  const names = (roles || []).map(rn);
  const run = (p, ok) => p.then(() => { setMsg(""); if (ok) toast(ok); load(); }).catch((e) => setMsg(e.message));
  return <><h2>Users</h2><Note err={err || msg} />
    <form className="card" onSubmit={(e) => { e.preventDefault(); run(http.post("/api/admin/users", f), "Staff account created."); setF({ ...f, password: "" }); }}>
      <h3>Create staff account</h3>
      <div className="grid-2">
        <div><label>Full name</label><input value={f.fullName} onChange={(e) => setF({ ...f, fullName: e.target.value })} required minLength={2} /></div>
        <div><label>Email</label><input type="email" value={f.email} onChange={(e) => setF({ ...f, email: e.target.value })} required /></div>
        <div><label>Password (10+ characters)</label><input type="password" value={f.password} onChange={(e) => setF({ ...f, password: e.target.value })} required minLength={10} /></div>
        <div><label>Role</label><select value={f.role} onChange={(e) => setF({ ...f, role: e.target.value })} required><option value="">Choose...</option>{names.map((n) => <option key={n}>{n}</option>)}</select></div>
      </div>
      <button className="primary">Create account</button>
    </form>
    <div className="tbl"><table><thead><tr><th>Name</th><th>Email</th><th>Roles</th><th>Status</th><th /></tr></thead>
      <tbody>{(rows || []).map((u) => <tr key={u.userId}><td>{u.fullName}</td><td>{u.email?.endsWith("@phone.invalid") ? "(phone only)" : u.email}</td>
        <td>{(u.roles || []).map((r) => <span className="pill rolepill" key={r}>{r} <a href="#" title="Remove role" onClick={(e) => { e.preventDefault(); run(http.del(`/api/admin/users/${u.userId}/roles/${r}`), "Role removed."); }}>×</a></span>)}
          <span className="addrole"><select value={pick[u.userId] || ""} onChange={(e) => setPick({ ...pick, [u.userId]: e.target.value })}><option value="">+ role</option>{names.filter((n) => !(u.roles || []).includes(n)).map((n) => <option key={n}>{n}</option>)}</select>
            {pick[u.userId] && <button onClick={() => run(http.post(`/api/admin/users/${u.userId}/roles`, { role: pick[u.userId] }), "Role added.").then?.(() => setPick({ ...pick, [u.userId]: "" }))}>Add</button>}</span></td>
        <td>{u.isActive ? "Active" : "Disabled"}</td>
        <td><button onClick={() => run(http.post(`/api/admin/users/${u.userId}/active`, { isActive: !u.isActive }))}>{u.isActive ? "Deactivate" : "Activate"}</button></td></tr>)}</tbody></table></div></>;
}

export function Organisations() {
  const [rows, err, load] = useLoad("/api/organisations");
  const [f, setF] = useState({ name: "", type: "NGO", country: "", contactEmail: "" }); const [msg, setMsg] = useState("");
  const admin = auth.role === "SystemAdmin", canAdd = admin || auth.role === "EmergencyCoordinator";
  const run = (p, ok) => p.then(() => { setMsg(""); toast(ok); load(); }).catch((e) => setMsg(e.message));
  return <><h2>Partner organisations</h2><Note err={err || msg} />
    {canAdd && <form className="card" onSubmit={(e) => { e.preventDefault(); run(http.post("/api/organisations", { ...f, country: f.country || null, contactEmail: f.contactEmail || null }), "Organisation added."); }}>
      <h3>Add organisation</h3><div className="grid-2">
        <div><label>Name</label><input value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} required minLength={2} /></div>
        <div><label>Type</label><input value={f.type} onChange={(e) => setF({ ...f, type: e.target.value })} required list="orgtypes" /><datalist id="orgtypes"><option>NGO</option><option>Government</option><option>Hospital</option><option>Company</option></datalist></div>
        <div><label>Country</label><input value={f.country} onChange={(e) => setF({ ...f, country: e.target.value })} /></div>
        <div><label>Contact email</label><input type="email" value={f.contactEmail} onChange={(e) => setF({ ...f, contactEmail: e.target.value })} /></div></div>
      <button className="primary">Add</button></form>}
    <div className="tbl"><table><thead><tr><th>Name</th><th>Type</th><th>Country</th><th>Status</th><th /></tr></thead>
      <tbody>{(rows || []).map((o) => { const id = o.organisationId ?? o.id; return <tr key={id}><td>{o.name}</td><td>{o.type}</td><td>{o.country ?? "-"}</td><td>{o.isActive === false ? "Disabled" : "Active"}</td>
        <td>{admin && <button onClick={() => run(http.patch(`/api/organisations/${id}/active`, { isActive: o.isActive === false }), "Updated.")}>{o.isActive === false ? "Enable" : "Disable"}</button>}</td></tr>; })}</tbody></table></div></>;
}

export function Crises() {
  const [rows, err, load] = useLoad("/api/crises");
  const [f, setF] = useState({ name: "", type: "Flood", severity: 3, status: "Active" }); const [msg, setMsg] = useState("");
  const coord = auth.role === "EmergencyCoordinator";
  const run = (p, ok) => p.then(() => { setMsg(""); toast(ok); load(); }).catch((e) => setMsg(e.message));
  return <><h2>Crises</h2><Note err={err || msg} />
    {coord && <form className="card" onSubmit={(e) => { e.preventDefault(); run(http.post("/api/crises", { ...f, severity: Number(f.severity) }), "Crisis created."); }}>
      <h3>New crisis</h3><div className="grid-2">
        <div><label>Name</label><input value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} required minLength={3} /></div>
        <div><label>Type</label><input value={f.type} onChange={(e) => setF({ ...f, type: e.target.value })} required list="ctypes" /><datalist id="ctypes"><option>Flood</option><option>Landslide</option><option>Earthquake</option><option>Disease outbreak</option></datalist></div>
        <div><label>Severity (1-5)</label><input type="number" min={1} max={5} value={f.severity} onChange={(e) => setF({ ...f, severity: e.target.value })} /></div>
        <div><label>Status</label><select value={f.status} onChange={(e) => setF({ ...f, status: e.target.value })}>{["Planned", "Active", "Contained", "Closed"].map((s) => <option key={s}>{s}</option>)}</select></div></div>
      <button className="primary">Create</button></form>}
    <div className="tbl"><table><thead><tr><th>Name</th><th>Type</th><th>Severity</th><th>Status</th><th>Started</th><th>Closed</th></tr></thead>
      <tbody>{(rows || []).map((c) => <tr key={c.crisisId}><td>{c.name}</td><td>{c.type}</td><td>{c.severity}</td>
        <td>{coord ? <select value={c.status} onChange={(e) => run(http.patch(`/api/crises/${c.crisisId}/status`, { status: e.target.value }), "Status updated.")}>{["Planned", "Active", "Contained", "Closed"].map((s) => <option key={s}>{s}</option>)}</select> : c.status}</td>
        <td>{fmt(c.startedAt)}</td><td>{fmt(c.closedAt)}</td></tr>)}</tbody></table></div>
    {coord && <p className="muted">Closing a crisis is refused while it still has unresolved requests.</p>}</>;
}

export function Duplicates() {
  const [rows, err, load] = useLoad("/api/duplicates?status=Pending"); const [msg, setMsg] = useState("");
  const res = (id, decision) => http.post(`/api/duplicates/${id}/resolve`, { decision }).then(() => { toast("Saved."); load(); }).catch((e) => setMsg(e.message));
  return <><h2>Possible duplicate requests</h2><p className="muted">Duplicates are flagged for a person to decide. Nothing is deleted automatically.</p><Note err={err || msg} />
    {rows && rows.length === 0 && <div className="card">No pending duplicates.</div>}
    {(rows || []).map((d) => <div className="card" key={d.duplicateId}>
      <b>Similarity {Math.round((d.similarityScore ?? 0) * 100)}%</b>
      <div className="grid-2">{[d.a, d.b].map((r) => <div className="ex-box" key={r.requestId}><b>{r.referenceNo}</b><p>{r.description}</p></div>)}</div>
      <div className="row"><button className="primary" onClick={() => res(d.duplicateId, "Confirmed")}>Confirm duplicate</button><button onClick={() => res(d.duplicateId, "NotDuplicate")}>Not a duplicate</button></div></div>)}</>;
}

export function Anomalies() {
  const [rows, err, load] = useLoad("/api/anomalies"); const [msg, setMsg] = useState("");
  const review = (id, clear) => http.post(`/api/anomalies/${id}/review`, { clear, note: clear ? "Checked, genuine request" : "Kept flagged" }).then(() => { toast("Saved."); load(); }).catch((e) => setMsg(e.message));
  return <><h2>Suspicious requests</h2><p className="muted">Flagged by rules, with the reason. A person decides.</p><Note err={err || msg} />
    {rows && rows.length === 0 && <div className="card">No flagged requests.</div>}
    {(rows || []).map((r) => <div className="card" key={r.requestId}>
      <b>{r.referenceNo}</b> · {r.category} · {r.peopleAffected} people <span className="pill">{r.status}</span>
      <p>{r.description}</p>{r.reasons && <p className="muted">Reason: {r.reasons}</p>}
      <div className="row"><button className="primary" onClick={() => review(r.requestId, true)}>Clear flag</button><button onClick={() => review(r.requestId, false)}>Keep flagged</button></div></div>)}</>;
}
