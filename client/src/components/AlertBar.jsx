import { useEffect, useState } from "react";
import { api } from "../api";

// Shows the latest active emergency alert at the top of every public page.
export default function AlertBar() {
  const [a, setA] = useState(null); const [hide, setHide] = useState(false);
  useEffect(() => { api.publicAlerts().then((l) => setA(Array.isArray(l) ? l[0] : null)).catch(() => {}); }, []);
  if (!a || hide) return null;
  return <div className={"alertbar " + String(a.severity || "").toLowerCase()} role="alert"><b>{a.title}</b> <span>{a.body}</span><button onClick={() => setHide(true)} aria-label="Dismiss">✕</button></div>;
}
