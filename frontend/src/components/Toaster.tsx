"use client";

import { useEffect, useState } from "react";

type ToastKind = "success" | "warning" | "error";
type ToastItem = { id: number; kind: ToastKind; text: string };

const EVENT = "app-toast";

export function toast(text: string, kind: ToastKind = "success") {
  window.dispatchEvent(new CustomEvent(EVENT, { detail: { text, kind } }));
}

export function Toaster() {
  const [items, setItems] = useState<ToastItem[]>([]);

  useEffect(() => {
    let next = 0;
    const timers = new Set<number>();
    function onToast(e: Event) {
      const { text, kind } = (e as CustomEvent<{ text: string; kind: ToastKind }>).detail;
      const id = next++;
      setItems((cur) => [...cur, { id, kind, text }]);
      const t = window.setTimeout(() => {
        timers.delete(t);
        setItems((cur) => cur.filter((x) => x.id !== id));
      }, 4500);
      timers.add(t);
    }
    window.addEventListener(EVENT, onToast);
    return () => {
      window.removeEventListener(EVENT, onToast);
      timers.forEach((t) => window.clearTimeout(t));
    };
  }, []);

  return (
    <div className="toast-stack" role="status" aria-live="polite">
      {items.map((t) => (
        <div key={t.id} className={`toast ${t.kind}`}>
          <span className="toast-icon" aria-hidden="true">{t.kind === "success" ? "✓" : "!"}</span>
          {t.text}
        </div>
      ))}
    </div>
  );
}
