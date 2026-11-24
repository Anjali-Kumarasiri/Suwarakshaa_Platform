// Same rules as the backend, so people see the problem before sending the form.
export const isEmail = (s) => /^[^@\s]+@[^@\s]+\.[^@\s]{2,}$/.test((s || "").trim());
export function normalizePhone(raw) {
  let s = (raw || "").replace(/[^\d+]/g, "");
  if (s.startsWith("00")) s = "+" + s.slice(2);
  if (s.startsWith("0") && s.length === 10) s = "+94" + s.slice(1);
  else if (s.startsWith("94") && s.length === 11) s = "+" + s;
  else if (s.startsWith("7") && s.length === 9) s = "+94" + s;
  return /^\+\d{9,15}$/.test(s) ? s : null;
}
