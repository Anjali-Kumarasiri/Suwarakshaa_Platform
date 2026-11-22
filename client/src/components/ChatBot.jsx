import { useEffect, useRef, useState } from "react";
import { api } from "../api";

const LANGS = [["en", "EN"], ["si", "සිං"], ["ta", "தமிழ்"]];
const HELLO = {
  en: "Hi! I can help you use Suwarakshaa: ask for help, track a request, find shelters or donate.",
  si: "ආයුබෝවන්! උදව් ඉල්ලන හැටි, ඉල්ලීමක තත්ත්වය, නවාතැන් සොයන හැටි හෝ පරිත්‍යාග කරන හැටි ගැන මගෙන් අසන්න.",
  ta: "வணக்கம்! உதவி கேட்பது, கோரிக்கை நிலை, தங்குமிடங்கள் அல்லது நன்கொடை பற்றி என்னிடம் கேளுங்கள்.",
};
const CHIPS = {
  en: ["How do I ask for help?", "Where are the shelters?", "How do I donate?"],
  si: ["උදව් ඉල්ලන්නේ කොහොමද?", "නවාතැන් කොහෙද?", "පරිත්‍යාග කරන්නේ කොහොමද?"],
  ta: ["உதவி கேட்பது எப்படி?", "தங்குமிடங்கள் எங்கே?", "நன்கொடை எப்படி?"],
};
const PLACEHOLDER = {
  en: "Type your question here...",
  si: "ඔබේ ප්‍රශ්නය මෙහි ටයිප් කරන්න...",
  ta: "உங்கள் கேள்வியை இங்கே தட்டச்சு செய்யுங்கள்...",
};
const MAX = 500;
const MAX_HEIGHT = 140;   // the box grows with the text up to this height, then scrolls

export default function ChatBot({ defaultOpen = false }) {
  const [open, setOpen] = useState(defaultOpen);
  const [lang, setLang] = useState("en");
  const [msgs, setMsgs] = useState([{ who: "bot", text: HELLO.en }]);
  const [input, setInput] = useState("");
  const [busy, setBusy] = useState(false);
  const end = useRef(null);
  const box = useRef(null);

  useEffect(() => { end.current?.scrollIntoView({ behavior: "smooth", block: "end" }); }, [msgs, open, busy]);
  useEffect(() => { if (open) box.current?.focus(); }, [open]);                    // cursor is ready as soon as the chat opens
  useEffect(() => {                                                                  // auto-grow the typing box
    const el = box.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(el.scrollHeight, MAX_HEIGHT)}px`;
  }, [input, open]);
  useEffect(() => {                                                                  // Esc closes the chat
    if (!open) return undefined;
    const onKey = (e) => { if (e.key === "Escape") setOpen(false); };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open]);

  function pickLang(l) { setLang(l); setMsgs((m) => [...m, { who: "bot", text: HELLO[l] }]); box.current?.focus(); }

  async function send(text) {
    const t = (text ?? input).trim();
    if (!t || busy) return;
    setInput(""); setBusy(true);
    setMsgs((m) => [...m, { who: "me", text: t }]);
    try {
      const r = await api.ask(t, lang);
      setMsgs((m) => [...m, { who: "bot", text: r.answer, warn: r.emergencyNotice, basic: r.usedFallback }]);
    } catch (e) {
      const text = /429/.test(e.message) ? "You are sending messages too quickly. Please wait a minute." : e.message;
      setMsgs((m) => [...m, { who: "bot", text, err: true }]);
    }
    setBusy(false);
    box.current?.focus();
  }

  // Enter = send, Shift+Enter = new line. While an input method (IME, e.g. Sinhala/Tamil) is composing, Enter must NOT send.
  function onKeyDown(e) {
    if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent?.isComposing) { e.preventDefault(); send(); }
  }

  if (!open) return <button className="chat-fab" onClick={() => setOpen(true)} aria-label="Open assistant">💬 Ask</button>;
  return (
    <section className="chat" aria-label="Assistant">
      <header>
        <strong>Suwarakshaa assistant</strong>
        <span className="chat-langs">{LANGS.map(([k, n]) => <button key={k} className={lang === k ? "on" : ""} onClick={() => pickLang(k)}>{n}</button>)}</span>
        <button onClick={() => setOpen(false)} aria-label="Close">✕</button>
      </header>
      <div className="chat-body" aria-live="polite">
        {msgs.map((m, i) => (
          <div key={i} className={"bubble " + m.who + (m.err ? " err" : "")}>
            {m.warn && <div className="chat-warn">⚠ {m.warn}</div>}
            {m.text}
            {m.basic && <small> (basic mode)</small>}
          </div>
        ))}
        {busy && <div className="bubble bot typing" aria-label="Assistant is typing"><span /><span /><span /></div>}
        {msgs.length < 3 && CHIPS[lang].map((c) => <button key={c} className="chat-chip" onClick={() => send(c)}>{c}</button>)}
        <div ref={end} />
      </div>
      <form className="chat-form" onSubmit={(e) => { e.preventDefault(); send(); }}>
        <textarea
          ref={box}
          className="chat-input"
          rows={1}
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={onKeyDown}
          maxLength={MAX}
          placeholder={PLACEHOLDER[lang]}
          aria-label="Type your question"
        />
        <button type="submit" className="chat-send" disabled={busy || !input.trim()}>Send</button>
      </form>
      <div className="chat-meta"><small className="chat-note">Automated assistant. It cannot approve help or give medical advice. In danger, call your local emergency number.</small>{input.length > MAX - 100 && <small className="chat-count">{input.length}/{MAX}</small>}</div>
    </section>
  );
}
