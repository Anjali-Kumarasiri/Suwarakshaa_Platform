import { useState } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { api, auth } from "../api";
import Brand from "../components/Brand";
import { isEmail, normalizePhone } from "../validate";

const ICON = {
  food: <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round"><path d="M8 3v7M5.5 3v5a2.5 2.5 0 0 0 5 0V3M8 10v11M17 21V3c-2.5 1.5-3.5 4-3.5 7.5H17" /></svg>,
  people: <svg viewBox="0 0 24 24" fill="currentColor"><circle cx="9" cy="8" r="3.2" /><circle cx="17" cy="9" r="2.6" /><path d="M3 20c0-3.6 2.7-6 6-6s6 2.4 6 6zM15.5 14.3c3 0 5.5 1.9 5.5 5.7h-4.3c0-2.2-.5-4-1.2-5.7z" /></svg>,
  heart: <svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 21s-8-5.2-8-11a4.5 4.5 0 0 1 8-2.8A4.5 4.5 0 0 1 20 10c0 5.8-8 11-8 11z" /></svg>,
};

export default function Login() {
  const nav = useNavigate();
  const from = useLocation().state?.from;
  const [mode, setMode] = useState("login");
  const [f, setF] = useState({ fullName: "", email: "", phone: "", identifier: "", password: "" });
  const [fe, setFe] = useState({});
  const [show, setShow] = useState(false);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const set = (k) => (e) => { setF({ ...f, [k]: e.target.value }); setFe({}); };
  const reg = mode === "register";

  function check() {
    const e = {};
    if (reg) {
      if (!f.email.trim() && !f.phone.trim()) e.contact = "Enter an email address, a phone number, or both.";
      if (f.email.trim() && !isEmail(f.email)) e.email = "Enter a valid email address, for example name@example.com.";
      if (f.phone.trim() && !normalizePhone(f.phone)) e.phone = "Enter a valid phone number, for example 077 123 4567.";
    } else {
      const id = f.identifier.trim();
      if (!id) e.identifier = "Enter your email or phone number.";
      else if (id.includes("@") ? !isEmail(id) : !normalizePhone(id)) e.identifier = id.includes("@") ? "That is not a valid email address." : "That is not a valid phone number.";
    }
    setFe(e);
    return Object.keys(e).length === 0;
  }

  async function submit(e) {
    e.preventDefault();
    setError("");
    if (!check()) return;
    setBusy(true);
    try {
      if (reg) await api.register({ fullName: f.fullName, email: f.email.trim() || null, phone: f.phone.trim() || null, password: f.password });
      const who = reg ? (f.email.trim() || normalizePhone(f.phone)) : f.identifier.trim();
      const { token, role, fullName } = await api.login(who, f.password);
      auth.save(token, role, fullName);
      nav(from || (role === "Citizen" ? "/request" : "/console"));
    } catch (err) { setError(err.message); }
    setBusy(false);
  }

  return (
    <div className="auth">
      <aside className="auth-side">
        <div className="panel-brand"><Brand light stack /></div>
        <hr />
        <ul>
          <li><i>{ICON.food}</i>Ask for food, medicine, water or shelter</li>
          <li><i>{ICON.people}</i>AI suggests, people make every decision</li>
          <li><i>{ICON.heart}</i>Donate and see how funds are used</li>
        </ul>
        <img className="auth-art" src="/images/auth-art.svg" alt="" />
        <p className="tagline">Neighbours helping neighbours ♡</p>
      </aside>
      <form className="auth-card" onSubmit={submit} noValidate>
        <div className="tabs">
          <button type="button" className={!reg ? "on" : ""} onClick={() => setMode("login")}>Log in</button>
          <button type="button" className={reg ? "on" : ""} onClick={() => setMode("register")}>Create account</button>
        </div>
        {reg ? (<>
          <label>Full name</label><input value={f.fullName} onChange={set("fullName")} required minLength={2} autoComplete="name" />
          <label>Email</label><input type="email" value={f.email} onChange={set("email")} autoComplete="email" placeholder="name@example.com" />
          {fe.email && <p className="error">{fe.email}</p>}
          <label>Phone number</label><input type="tel" value={f.phone} onChange={set("phone")} autoComplete="tel" placeholder="077 123 4567" />
          {fe.phone && <p className="error">{fe.phone}</p>}
          {fe.contact && <p className="error">{fe.contact}</p>}
          <p className="muted">Enter your email, your phone number, or both. You can log in with either.</p>
        </>) : (<>
          <label>Email or phone number</label><input value={f.identifier} onChange={set("identifier")} autoComplete="username" placeholder="name@example.com or 077 123 4567" />
          {fe.identifier && <p className="error">{fe.identifier}</p>}
        </>)}
        <label>Password</label>
        <div className="pw">
          <input type={show ? "text" : "password"} value={f.password} onChange={set("password")} required minLength={reg ? 8 : 1} autoComplete={reg ? "new-password" : "current-password"} />
          <button type="button" onClick={() => setShow(!show)}>{show ? "Hide" : "Show"}</button>
        </div>
        {reg && <p className="muted">At least 8 characters, with letters and digits.</p>}
        {error && <p className="error">{error}</p>}
        <button className="primary" disabled={busy}>{busy ? "Please wait..." : reg ? "Create account" : "Log in"}</button>
      </form>
    </div>
  );
}
