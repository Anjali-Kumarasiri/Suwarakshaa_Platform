import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../api";
import { toast } from "../toast";

const CATEGORIES = [["Medicine", "💊"], ["Food", "🍚"], ["Water", "💧"], ["Shelter", "🏠"], ["Transport", "🚐"], ["Accessibility", "♿"], ["Other", "📝"]];

export default function CitizenRequest() {
  const [category, setCategory] = useState("Medicine");
  const [description, setDescription] = useState("");
  const [peopleAffected, setPeople] = useState(1);
  const [address, setAddress] = useState("");
  const [urgency, setUrgency] = useState("");
  const [coords, setCoords] = useState(null);
  const [status, setStatus] = useState(null);
  const [mine, setMine] = useState([]);

  const load = () => api.myRequests().then(setMine).catch(() => {});
  useEffect(() => { load(); }, []);

  const useMyLocation = () => navigator.geolocation.getCurrentPosition(
    (p) => setCoords({ latitude: p.coords.latitude, longitude: p.coords.longitude }),
    () => setStatus({ ok: false, msg: "Could not get your location. Type an address instead." }));

  async function submit(e) {
    e.preventDefault();
    setStatus(null);
    try {
      const r = await api.submitRequest({
        category, description, peopleAffected: Number(peopleAffected),
        address: address || null, userUrgency: urgency ? Number(urgency) : null, ...coords,
      });
      setStatus({ ok: true, msg: `Request received. Reference: ${r.referenceNo}` });
      toast(`Request received. Reference ${r.referenceNo}. We will notify you of updates.`);
      setDescription(""); setAddress(""); load();
    } catch (err) { setStatus({ ok: false, msg: err.message }); }
  }

  return (<>
    <form className="card" onSubmit={submit}>
      <h2>Request help</h2>
      <div className="grid2">
        {CATEGORIES.map(([c, icon]) => (
          <button type="button" key={c} className={"tile" + (category === c ? " active" : "")} onClick={() => setCategory(c)}>{icon} {c}</button>
        ))}
      </div>
      <label>What do you need? (at least 10 characters)</label>
      <textarea rows={4} value={description} onChange={(e) => setDescription(e.target.value)} required minLength={10} />
      <label>People affected</label>
      <input type="number" min={1} max={1000} value={peopleAffected} onChange={(e) => setPeople(e.target.value)} />
      <label>Address (optional, we find it on the map)</label>
      <input value={address} onChange={(e) => setAddress(e.target.value)} />
      <label>How urgent is it? (optional)</label>
      <select value={urgency} onChange={(e) => setUrgency(e.target.value)}>
        <option value="">Not sure</option><option value="1">1 - Low</option><option value="2">2</option><option value="3">3 - Medium</option><option value="4">4</option><option value="5">5 - Life threatening</option>
      </select>
      <div className="row">
        <button type="button" onClick={useMyLocation}>{coords ? "Location saved ✓" : "Use my location"}</button>
      </div>
      {status && <p className={status.ok ? "muted" : "error"}>{status.msg}</p>}
      <button className="primary">Submit request</button>
    </form>

    <div className="card">
      <h2>My requests</h2>
      {mine.length === 0 && <p className="muted">No requests yet.</p>}
      {mine.map((r) => (
        <p key={r.requestId}><Link to={`/request/${r.requestId}`}><strong>{r.referenceNo}</strong></Link> · {r.category} · <span className="badge">{r.status}</span><br />
          <span className="muted">{r.description}</span></p>
      ))}
    </div>
  </>);
}
