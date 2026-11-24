// toast("message") shows a pop-up from anywhere in the app. type: "ok" | "err"
export const toast = (text, type = "ok") => window.dispatchEvent(new CustomEvent("toast", { detail: { text, type } }));
