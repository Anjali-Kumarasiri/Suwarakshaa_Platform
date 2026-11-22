import { useEffect, useState } from "react";
import { Link, NavLink, Outlet, useLocation, useNavigate, useParams } from "react-router-dom";
import { api, auth } from "../api";
import Brand from "../components/Brand";

const MENU = [
  ["/console", "Dashboard", ["EmergencyCoordinator", "CaseManager", "SystemAdmin", "LogisticsManager", "ShelterAdmin"], true],
  ["/console/queue", "Review queue", ["EmergencyCoordinator", "CaseManager"]],
  ["/console/duplicates", "Duplicates", ["EmergencyCoordinator", "CaseManager"]],
  ["/console/anomalies", "Suspicious", ["EmergencyCoordinator", "CaseManager"]],
  ["/console/crises", "Crises", ["EmergencyCoordinator", "CaseManager", "SystemAdmin"]],
  ["/console/organisations", "Organisations", ["EmergencyCoordinator", "SystemAdmin"]],
  ["/console/analytics", "Analytics", ["EmergencyCoordinator", "CaseManager", "SystemAdmin"]],
  ["/console/requests", "All requests", ["EmergencyCoordinator", "CaseManager"]],
  ["/console/shelters", "Shelters", ["ShelterAdmin", "EmergencyCoordinator", "CaseManager"]],
  ["/console/resources", "Resources", ["LogisticsManager", "EmergencyCoordinator"]],
  ["/console/volunteers", "Volunteers", ["VolunteerCoordinator", "EmergencyCoordinator"]],
  ["/console/donations", "Donations", ["DonorOfficer", "EmergencyCoordinator", "SystemAdmin"]],
  ["/console/alerts", "Alerts", ["EmergencyCoordinator"]],
  ["/console/review", "AI review", ["EmergencyCoordinator", "CaseManager", "SystemAdmin"]],
  ["/console/users", "Users", ["SystemAdmin"]],
  ["/console/audit", "Audit log", ["SystemAdmin", "EmergencyCoordinator"]],
];
const label = (k) => k.replace(/([A-Z])/g, " $1").replace(/^./, (c) => c.toUpperCase());
const flat = (o, p = "") => Object.entries(o || {}).flatMap(([k, v]) =>
  v && typeof v === "object" && !Array.isArray(v) ? flat(v, p + label(k) + " · ") : [[p + label(k), Array.isArray(v) ? `${v.length} items` : String(v)]]);

export default function Console() {
  const nav = useNavigate();
  const path = useLocation().pathname;
  const page = path.startsWith("/console/cases") ? "Cases" : (MENU.find(([to]) => path === to || (to !== "/console" && path.startsWith(to + "/"))) || [])[1] || "Console";
  const links = MENU.filter(([, , roles]) => roles.includes(auth.role));
  return (
    <div className="console-wrap">
      <aside className="side">
        <div className="side-brand"><Brand light stack /><small>Staff console</small></div>
        <nav>{links.map(([to, name, , end]) => <NavLink key={to} to={to} end={end}>{name}</NavLink>)}
          <NavLink to="/map">Shelters map</NavLink></nav>
        <div className="who"><b>{auth.name}</b><small>{auth.role}</small>
          <button onClick={() => { auth.clear(); nav("/login"); }}>Log out</button></div>
      </aside>
      <section className="content">
        <div className="topbar2"><div className="crumbs">Staff console <span>/</span> <b>{page}</b></div>
          <div className="top-right"><Link to="/">Public site</Link><span className="chip">{(auth.name || "?").slice(0, 1).toUpperCase()}</span></div></div>
        {links.length ? <Outlet /> : <div className="card">Welcome, {auth.name}. Your tools for the {auth.role} role will appear here.</div>}</section>
    </div>
  );
}

const COORD = ["EmergencyCoordinator", "CaseManager"];
// label + plain-language help for every dashboard number; "to" makes the card clickable
const METRICS = {
  unresolvedCases: { label: "Unresolved cases", help: "Requests not yet fulfilled, closed, rejected or merged.", to: "/console/cases/unresolved" },
  overdueCases: { label: "Overdue cases", help: "Open requests that passed their response deadline.", to: "/console/cases/overdue" },
  escalatedCases: { label: "Escalated cases", help: "Requests marked as needing urgent attention.", to: "/console/cases/escalated" },
  pendingAiReviews: { label: "Pending AI reviews", help: "AI suggestions waiting for a person to decide.", to: "/console/cases/pendingAi" },
  aiFallbackCount: { label: "AI fallback count", help: "Suggestions made by backup rules because the AI service was unavailable.", to: "/console/cases/aiFallback" },
  suspiciousRequests: { label: "Suspicious requests", help: "Requests flagged for a possible duplicate or misuse.", to: "/console/cases/suspicious" },
  aiOverrideRatePct: { label: "AI override rate", unit: "%", help: "Share of decisions where people modified or rejected the AI suggestion." },
  totalDecisions: { label: "Total decisions", help: "Approve, modify, reject or defer decisions made so far." },
  shelterOccupancyPct: { label: "Shelter occupancy", unit: "%", help: "How full the registered shelters are.", to: "/map" },
  resourceUtilisationPct: { label: "Resource utilisation", unit: "%", help: "Share of stock that is already allocated." },
  avgResolutionMinutes: { label: "Avg resolution time", unit: " min", help: "Average time from request to fulfilled or closed." },
  pendingDuplicateReviews: { label: "Pending duplicate reviews", help: "Possible duplicate requests waiting for review." },
  notificationDeadLetters: { label: "Failed notifications", help: "Notifications that could not be delivered after several tries." },
};

export function Dashboard() {
  const [m, setM] = useState(null); const [err, setErr] = useState("");
  useEffect(() => { api.metrics().then(setM).catch((e) => setErr(e.message)); }, []);
  if (err) return <p className="error">{err}</p>;
  if (!m) return <p className="muted">Loading...</p>;
  return (<><h2>Dashboard</h2><p className="muted">Click a card with an arrow to see the records behind the number.</p>
    <div className="stat-grid">{Object.entries(m).map(([k, v]) => {
      const d = METRICS[k] || { label: label(k), help: "" };
      const val = v === null || v === undefined ? "-" : `${v}${d.unit || ""}`;
      const body = <><strong>{val}</strong><span>{d.label}</span><small>{d.help}</small>{d.to && <i className="go">→</i>}</>;
      return d.to ? <Link key={k} to={d.to} className="stat link">{body}</Link> : <div key={k} className="stat">{body}</div>;
    })}</div></>);
}

const TITLES = { unresolved: "Unresolved cases", overdue: "Overdue cases", escalated: "Escalated cases", suspicious: "Suspicious requests", pendingAi: "Pending AI reviews", aiFallback: "AI fallback suggestions" };
const when = (d) => (d ? new Date(d).toLocaleString() : "-");

export function Cases() {
  const { filter } = useParams();
  const [res, setRes] = useState(null); const [err, setErr] = useState("");
  useEffect(() => { setRes(null); api.cases(filter).then(setRes).catch((e) => setErr(e.message)); }, [filter]);
  return (<>
    <Link to="/console" className="back">← Dashboard</Link>
    <h2>{TITLES[filter] || "Cases"}</h2>
    {err && <p className="error">{err}</p>}
    {!res && !err && <p className="muted">Loading...</p>}
    {res && res.items.length === 0 && <div className="card">Nothing here right now.</div>}
    {res && res.items.length > 0 && (res.kind === "requests" ? (
      <div className="tbl"><table><thead><tr><th>Reference</th><th>Category</th><th>People</th><th>Priority</th><th>Status</th><th>Due</th><th>Created</th></tr></thead>
        <tbody>{res.items.map((r) => <tr key={r.requestId}><td><b>{r.referenceNo}</b></td><td>{r.category}</td><td>{r.peopleAffected}</td><td>{r.finalPriority ?? "-"}</td><td><span className="pill">{r.status}</span></td><td>{when(r.dueAt)}</td><td>{when(r.createdAt)}</td></tr>)}</tbody></table></div>
    ) : (
      <div className="tbl"><table><thead><tr><th>Suggestion</th><th>Request</th><th>Type</th><th>Confidence</th><th>Source</th><th>Status</th><th>Created</th></tr></thead>
        <tbody>{res.items.map((r) => <tr key={r.recommendationId}><td>#{r.recommendationId}</td><td>{r.requestId ? `#${r.requestId}` : "-"}</td><td>{r.type}</td><td>{Math.round((r.confidence ?? 0) * 100)}%</td><td>{r.isFallback ? "Backup rules" : "AI"}</td><td><span className="pill">{r.status}</span></td><td>{when(r.createdAt)}</td></tr>)}</tbody></table></div>
    ))}
    {res && filter === "pendingAi" && COORD.includes(auth.role) && <p><Link to="/console/queue" className="cta-btn">Open the review queue</Link></p>}
  </>);
}

export function Users() {
  const [rows, setRows] = useState([]); const [err, setErr] = useState("");
  const load = () => api.users().then(setRows).catch((e) => setErr(e.message));
  useEffect(() => { load(); }, []);
  const toggle = (u) => api.setActive(u.userId, !u.isActive).then(load).catch((e) => setErr(e.message));
  return (<><h2>Users</h2>{err && <p className="error">{err}</p>}
    <div className="tbl"><table><thead><tr><th>Name</th><th>Email</th><th>Roles</th><th>Status</th><th /></tr></thead>
      <tbody>{rows.map((u) => <tr key={u.userId}><td>{u.fullName}</td><td>{u.email}</td><td>{(u.roles || []).join(", ")}</td>
        <td>{u.isActive ? "Active" : "Disabled"}</td><td><button onClick={() => toggle(u)}>{u.isActive ? "Deactivate" : "Activate"}</button></td></tr>)}</tbody></table></div></>);
}

export function Audit() {
  const [rows, setRows] = useState([]); const [err, setErr] = useState("");
  useEffect(() => { api.audit().then((r) => setRows(r.items || [])).catch((e) => setErr(e.message)); }, []);
  return (<><h2>Audit log</h2><p className="muted">Read-only. Records cannot be edited or deleted.</p>{err && <p className="error">{err}</p>}
    <div className="tbl"><table><thead><tr><th>Time</th><th>User</th><th>Action</th><th>Entity</th></tr></thead>
      <tbody>{rows.map((a) => <tr key={a.auditId}><td>{new Date(a.occurredAt).toLocaleString()}</td><td>{a.userId ?? "-"}</td><td>{a.action}</td><td>{a.entityType} {a.entityId}</td></tr>)}</tbody></table></div></>);
}
