import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api, auth } from "../api";
import { toast } from "../toast";

const fmt = (n, c = "LKR") => `${c} ${Number(n || 0).toLocaleString()}`;

export default function Donate() {
  const nav = useNavigate();
  const [camps, setCamps] = useState([]);
  const [cid, setCid] = useState("");
  const [amount, setAmount] = useState(5000);
  const [currency, setCurrency] = useState("LKR");
  const [anon, setAnon] = useState(false);
  const [trans, setTrans] = useState(null);
  const [mine, setMine] = useState([]);
  const [msg, setMsg] = useState(null);

  useEffect(() => {
    api.campaigns().then((c) => { setCamps(c); if (c[0]) setCid(String(c[0].campaignId)); }).catch((e) => setMsg({ ok: false, text: e.message }));
    if (auth.token) api.myDonations().then(setMine).catch(() => {});
  }, []);
  useEffect(() => { if (cid) api.transparency(cid).then(setTrans).catch(() => setTrans(null)); }, [cid]);

  async function submit(e) {
    e.preventDefault();
    if (!auth.token) return nav("/login", { state: { from: "/donate" } });
    setMsg(null);
    try {
      const r = await api.donate({ campaignId: Number(cid), type: "Monetary", amount: Number(amount), currency, anonymous: anon });
      setMsg({ ok: true, text: `Thank you! Donation recorded (ref ${r.paymentRef}).${r.flagged ? " It was flagged for a routine review." : ""}` });
      toast("Thank you! Your donation was recorded.");
      api.myDonations().then(setMine); api.transparency(cid).then(setTrans);
    } catch (err) { setMsg({ ok: false, text: err.message }); }
  }

  return (
    <div className="donate-page">
      <h2>Support relief efforts</h2>
      <div className="camp-list">
        {camps.length === 0 && <p className="muted">No active campaigns right now.</p>}
        {camps.map((c) => (
          <button key={c.campaignId} type="button" className={"camp" + (String(c.campaignId) === cid ? " on" : "")} onClick={() => setCid(String(c.campaignId))}>
            <strong>{c.name}</strong>
            <div className="bar-bg"><div style={{ width: Math.min(c.pct || 0, 100) + "%" }} /></div>
            <span className="muted">{fmt(c.raised)} raised of {fmt(c.targetAmount)} ({c.pct}%)</span>
          </button>
        ))}
      </div>

      <div className="grid-2">
        <form className="card" onSubmit={submit}>
          <h3>Donation details</h3>
          <label>Amount</label>
          <div className="chips">{[1000, 5000, 10000, 25000].map((n) => <button type="button" key={n} className={amount === n ? "on" : ""} onClick={() => setAmount(n)}>{n.toLocaleString()}</button>)}</div>
          <div className="row">
            <input type="number" min={1} value={amount} onChange={(e) => setAmount(Number(e.target.value))} required />
            <select value={currency} onChange={(e) => setCurrency(e.target.value)}><option>LKR</option><option>USD</option></select>
          </div>
          <label className="check"><input type="checkbox" checked={anon} onChange={(e) => setAnon(e.target.checked)} /> Donate anonymously</label>
          {msg && <p className={msg.ok ? "ok" : "error"}>{msg.text}</p>}
          <button className="primary" disabled={!cid}>{auth.token ? "Donate" : "Log in to donate"}</button>
          <p className="muted">Sandbox mode: no real payment is taken.</p>
        </form>

        <div className="card">
          <h3>Where the money goes</h3>
          {!trans ? <p className="muted">Choose a campaign.</p> : (<>
            <p>Raised <strong>{fmt(trans.raised)}</strong> · Spent <strong>{fmt(trans.spent)}</strong> · Left <strong>{fmt(trans.remaining)}</strong></p>
            {trans.spending.length === 0 ? <p className="muted">Spending will appear here as funds are used.</p>
              : trans.spending.map((s, i) => <p key={i} className="muted">{fmt(s.amountUsed)} · {s.purpose}</p>)}
          </>)}
          {mine.length > 0 && (<><h3>My donations</h3>{mine.map((d) => <p key={d.donationId} className="muted">{new Date(d.receivedAt).toLocaleDateString()} · {fmt(d.amount, d.currency)}</p>)}</>)}
        </div>
      </div>
    </div>
  );
}
