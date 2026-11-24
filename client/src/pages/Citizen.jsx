import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { http, upload, download } from "../api";
import { toast } from "../toast";
import { Table, Note, head, fmt, useLoad } from "./Manage";

export function RequestDetail() {
  const { id } = useParams();
  const [d, err] = useLoad(`/api/requests/${id}`);
  const [files, , loadF] = useLoad(`/api/requests/${id}/attachments`);
  const [fb, setFb] = useState({ rating: 5, comment: "" }); const [msg, setMsg] = useState(""); const [sent, setSent] = useState(false);
  const done = d && ["Fulfilled", "Closed"].includes(fmt(d.status));
  const go = (p, ok) => p.then(() => { setMsg(""); toast(ok); }).catch((e) => setMsg(e.message));
  return <><Link className="back" to="/request">← My requests</Link><h2>{d?.referenceNo ?? "Request"}</h2><Note err={err || msg} />
    {d && <div className="card"><span className="pill">{fmt(d.status)}</span> {d.category} · {d.peopleAffected} people<p>{d.description}</p>
      {Object.entries(d).filter(([, v]) => Array.isArray(v)).map(([k, v]) => <div key={k}><h3>{k === "history" ? "Timeline" : head(k)}</h3><Table rows={v} /></div>)}</div>}
    <div className="card"><h3>Photos and documents</h3>
      <p className="muted">Add a photo or document that helps (JPG, PNG or PDF).</p>
      <input type="file" accept="image/*,.pdf" onChange={(e) => e.target.files[0] && go(upload(id, e.target.files[0]).then(loadF), "File uploaded.")} />
      {(files || []).map((a) => { const aid = a.attachmentId ?? a.id; return <p key={aid}>{a.fileName ?? a.originalName ?? "file"} <button onClick={() => go(download(id, aid), "Opened.")}>Open</button></p>; })}</div>
    {done && !sent && <form className="card" onSubmit={(e) => { e.preventDefault(); go(http.post(`/api/requests/${id}/feedback`, { rating: Number(fb.rating), comment: fb.comment || null }).then(() => setSent(true)), "Thank you for your feedback."); }}>
      <h3>How was the help you received?</h3>
      <select value={fb.rating} onChange={(e) => setFb({ ...fb, rating: e.target.value })}>{[5, 4, 3, 2, 1].map((n) => <option key={n} value={n}>{n} {n === 5 ? "(great)" : n === 1 ? "(poor)" : ""}</option>)}</select>
      <label>Comment (optional)</label><textarea rows={3} value={fb.comment} onChange={(e) => setFb({ ...fb, comment: e.target.value })} /><button className="primary">Send feedback</button></form>}</>;
}

export function Profile() {
  const [f, setF] = useState({ memberCount: 1, hasChildren: false, hasElderly: false, hasDisability: false, medicalNotes: "", address: "" }); const [msg, setMsg] = useState("");
  useEffect(() => { http.get("/api/households/me").then((d) => d && setF((x) => ({ ...x, ...d, medicalNotes: d.medicalNotes ?? "", address: d.address ?? "" }))).catch(() => {}); }, []);
  const set = (k) => (e) => setF({ ...f, [k]: e.target.type === "checkbox" ? e.target.checked : e.target.value });
  const save = (e) => { e.preventDefault(); http.put("/api/households/me", { memberCount: Number(f.memberCount), hasChildren: f.hasChildren, hasElderly: f.hasElderly, hasDisability: f.hasDisability, medicalNotes: f.medicalNotes || null, address: f.address || null }).then(() => { setMsg(""); toast("Profile saved."); }).catch((x) => setMsg(x.message)); };
  return <form className="card" onSubmit={save}><h2>My household</h2><p className="muted">Helps coordinators prioritise. Medical notes and address are stored encrypted.</p><Note err={msg} />
    <label>People in your household</label><input type="number" min={1} value={f.memberCount} onChange={set("memberCount")} />
    {[["hasChildren", "Children under 12"], ["hasElderly", "Elderly people"], ["hasDisability", "Someone with a disability"]].map(([k, n]) => <label className="check" key={k}><input type="checkbox" checked={f[k]} onChange={set(k)} /> {n}</label>)}
    <label>Medical notes (optional)</label><textarea rows={3} value={f.medicalNotes} onChange={set("medicalNotes")} />
    <label>Home address (optional)</label><input value={f.address} onChange={set("address")} /><button className="primary">Save</button></form>;
}

export function Volunteer() {
  const [skills] = useLoad("/api/volunteers/skills");
  const [me, meErr, loadMe] = useLoad("/api/volunteers/me");
  const [jobs, , loadJobs] = useLoad("/api/volunteers/assignments/mine");
  const [sel, setSel] = useState({}); const [car, setCar] = useState(false); const [pos, setPos] = useState({});
  const [av, setAv] = useState({ startsAt: "", endsAt: "" }); const [msg, setMsg] = useState("");
  const go = (p, ok, after) => p.then(() => { setMsg(""); toast(ok); after?.(); }).catch((e) => setMsg(e.message));
  const sid = (s) => s.skillId ?? s.id;
  const where = () => navigator.geolocation.getCurrentPosition((p) => setPos({ baseLatitude: p.coords.latitude, baseLongitude: p.coords.longitude }));
  return <><h2>Volunteer</h2><Note err={msg} />
    {meErr ? <form className="card" onSubmit={(e) => { e.preventDefault(); go(http.post("/api/volunteers/register", { ...pos, hasVehicle: car, skills: Object.keys(sel).filter((k) => sel[k]).map((k) => ({ skillId: Number(k), level: 2 })) }), "Registered. A coordinator will verify you.", loadMe); }}>
      <h3>Register as a volunteer</h3><p className="muted">Choose your skills so tasks can be matched to you.</p>
      <div className="chips">{(skills || []).map((s) => <button type="button" key={sid(s)} className={sel[sid(s)] ? "on" : ""} onClick={() => setSel({ ...sel, [sid(s)]: !sel[sid(s)] })}>{s.name}</button>)}</div>
      <label className="check"><input type="checkbox" checked={car} onChange={(e) => setCar(e.target.checked)} /> I have a vehicle</label>
      <button type="button" onClick={where}>{pos.baseLatitude ? "Location saved ✓" : "Use my location as my base"}</button> <button className="primary">Register</button></form>
    : me && <div className="card"><h3>Your volunteer profile</h3><p>Status: <span className="pill">{fmt(me.verificationStatus ?? me.status ?? "Registered")}</span></p></div>}
    {!meErr && <form className="card" onSubmit={(e) => { e.preventDefault(); go(http.post("/api/volunteers/availability", { startsAt: new Date(av.startsAt).toISOString(), endsAt: new Date(av.endsAt).toISOString() }), "Availability added."); }}>
      <h3>When can you help?</h3><div className="grid-2"><div><label>From</label><input type="datetime-local" value={av.startsAt} onChange={(e) => setAv({ ...av, startsAt: e.target.value })} required /></div>
      <div><label>To</label><input type="datetime-local" value={av.endsAt} onChange={(e) => setAv({ ...av, endsAt: e.target.value })} required /></div></div><button className="primary">Add</button></form>}
    {jobs && jobs.length > 0 && <><h3>My tasks</h3><div className="tbl"><table><thead><tr><th>Task</th><th>Status</th><th /></tr></thead><tbody>{jobs.map((j) => { const aid = j.assignmentId ?? j.id; return <tr key={aid}><td>{j.title ?? j.taskTitle ?? `Task ${j.taskId}`}</td><td>{fmt(j.status)}</td>
      <td><button onClick={() => go(http.post(`/api/volunteers/assignments/${aid}/respond`, { accept: true }), "Accepted.", loadJobs)}>Accept</button> <button onClick={() => go(http.post(`/api/volunteers/assignments/${aid}/respond`, { accept: false }), "Declined.", loadJobs)}>Decline</button></td></tr>; })}</tbody></table></div></>}</>;
}
