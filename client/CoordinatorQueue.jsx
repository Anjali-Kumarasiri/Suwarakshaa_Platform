import { useEffect, useState } from "react";
import { api, auth } from "../api";

const CATS = ["Food", "Medicine", "Shelter", "Transport", "Accessibility", "Water", "Other"];
const pretty = (s) => { try { return JSON.stringify(JSON.parse(s), null, 2); } catch { return s; } };

export default function CoordinatorQueue() {
  const [items, setItems] = useState([]);
  const [waiting, setWaiting] = useState([]);
  const [reasons, setReasons] = useState({});
  const [mods, setMods] = useState({});     // per recommendation: { category, urgency } used by Modify
  const [msg, setMsg] = useState(null);
  const isCoord = auth.role === "EmergencyCoordinator";

  async function load() {
    try {
      setItems(await api.pendingRecommendations());
      if (isCoord) setWaiting(await api.pendingApproval());
    } catch (e) { setMsg({ ok: false, text: e.message }); }
  }
  useEffect(() => { load(); }, []);

  async function decide(it, action) {
    const reason = reasons[it.recommendationId] || "";
    if ((action === "Modify" || action === "Reject") && !reason.trim()) return setMsg({ ok: false, text: "A reason is required to modify or reject." });
    const m = mods[it.recommendationId] || {};
    const finalValue = action === "Modify" ? JSON.stringify({ category: m.category || it.category, urgency: Number(m.urgency || 3) }) : null;
    try {
      const r = await api.decide(it.recommendationId, action, finalValue, reason);
      setMsg({ ok: true, text: r.message });
      load();
    } catch (e) { setMsg({ ok: false, text: e.message }); }
  }

  async function second(d, approve) {
    try { await api.secondApproval(d.recommendationId, approve, approve ? "Approved by second coordinator" : "Refused by second coordinator"); setMsg({ ok: true, text: approve ? "Second approval recorded." : "Decision refused." }); load(); }
    catch (e) { setMsg({ ok: false, text: e.message }); }
  }

  return (<>
    <h2>Review queue</h2>
    {msg && <p className={msg.ok ? "muted" : "error"}>{msg.text}</p>}
    <p className="muted">{items.length} AI suggestion(s) waiting. AI only suggests: you decide.</p>

    {items.map((it) => {
      const id = it.recommendationId, m = mods[id] || {};
      return (
        <div className="card" key={id}>
          <strong>Request #{it.requestId}</strong> · {it.category} · {it.peopleAffected} people {it.isFallback && <span className="badge warn">Rule-based fallback</span>}
          <p>{it.description}</p>
          <div className="suggestion">
            <div>AI suggestion ({it.type}) · confidence {Math.round((it.confidence ?? 0) * 100)}%</div>
            <pre>{pretty(it.outputJson)}</pre>
            {it.rationale && <p className="muted">{it.rationale}</p>}
          </div>
          <label>Reason (required for Modify / Reject)</label>
          <input value={reasons[id] || ""} onChange={(e) => setReasons({ ...reasons, [id]: e.target.value })} />
          <div className="row">
            <select value={m.category || it.category} onChange={(e) => setMods({ ...mods, [id]: { ...m, category: e.target.value } })}>{CATS.map((c) => <option key={c}>{c}</option>)}</select>
            <select value={m.urgency || 3} onChange={(e) => setMods({ ...mods, [id]: { ...m, urgency: e.target.value } })}>{[1, 2, 3, 4, 5].map((n) => <option key={n} value={n}>Urgency {n}</option>)}</select>
          </div>
          <div className="row">
            <button className="primary" onClick={() => decide(it, "Approve")}>Approve</button>
            <button onClick={() => decide(it, "Modify")}>Modify</button>
            <button onClick={() => decide(it, "Reject")}>Reject</button>
            <button onClick={() => decide(it, "Defer")}>Defer</button>
          </div>
        </div>);
    })}

    {isCoord && waiting.length > 0 && (<>
      <h2>Waiting for second approval</h2>
      {waiting.map((d) => (
        <div className="card" key={d.recommendationId}>
          <strong>Request #{d.requestId}</strong> · proposed <span className="badge warn">{d.proposedAction}</span> by {d.proposedBy}
          <p className="muted">{d.reason}</p>
          <div className="row"><button className="primary" onClick={() => second(d, true)}>Approve</button><button onClick={() => second(d, false)}>Refuse</button></div>
        </div>))}
    </>)}
  </>);
}
