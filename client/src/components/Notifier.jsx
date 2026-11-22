import { useEffect, useRef, useState } from "react";
import { useLocation } from "react-router-dom";
import { api, auth } from "../api";

// Pop-up toasts + a bell with the user's in-app notifications (status updates on their requests).
export default function Notifier() {
  const loc = useLocation();
  const [toasts, setToasts] = useState([]);
  const [items, setItems] = useState([]);
  const [unread, setUnread] = useState(0);
  const [open, setOpen] = useState(false);
  const poll = useRef(() => {});

  const push = (text, type = "ok") => {
    const id = Math.random();
    setToasts((t) => [...t, { id, text, type }]);
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), 6500);
  };
  useEffect(() => {
    const h = (e) => push(e.detail.text, e.detail.type);
    window.addEventListener("toast", h);
    return () => window.removeEventListener("toast", h);
  }, []);

  poll.current = async () => {
    if (!auth.token) return;
    try {
      const list = await api.notifications();
      setItems(list);
      const key = "lastNotif:" + auth.name;
      const last = Number(localStorage.getItem(key) ?? -1);
      const max = list.reduce((m, n) => Math.max(m, n.notificationId), 0);
      if (last >= 0) {
        const fresh = list.filter((n) => n.notificationId > last);
        fresh.forEach((n) => push(`${n.subject}: ${n.body}`));
        if (fresh.length) setUnread((u) => u + fresh.length);
      }
      localStorage.setItem(key, String(max));
    } catch { /* offline or not allowed: ignore */ }
  };
  useEffect(() => { poll.current(); }, [loc.pathname]);
  useEffect(() => { const id = setInterval(() => poll.current(), 20000); return () => clearInterval(id); }, []);

  return (
    <>
      <div className="toasts" aria-live="polite">
        {toasts.map((t) => <div key={t.id} className={"toast " + t.type} onClick={() => setToasts((x) => x.filter((y) => y.id !== t.id))}>{t.text}</div>)}
      </div>
      {auth.token && (
        <div className="bell-wrap">
          {open && (
            <div className="bell-panel">
              <strong>Notifications</strong>
              {items.length === 0 && <p className="muted">Nothing yet.</p>}
              {items.slice(0, 10).map((n) => <div className="bell-item" key={n.notificationId}><b>{n.subject}</b><p>{n.body}</p><small>{new Date(n.createdAt).toLocaleString()}</small></div>)}
            </div>
          )}
          <button className="bell" onClick={() => { setOpen(!open); setUnread(0); }} aria-label="Notifications">🔔{unread > 0 && <i>{unread}</i>}</button>
        </div>
      )}
    </>
  );
}
