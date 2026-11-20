"use client";

import { useEffect, useState } from "react";
import { api, errMessage } from "@/lib/api";
import { PageHeader } from "@/components/PageHeader";

type Commitment = {
  id: string;
  action: string;
  ownerName: string;
  dueDate?: string;
  status: string;
  meetingTitle?: string;
  decisionStatement?: string;
  task?: { externalTaskId: string; status: string; failureReason?: string; destinationName: string };
  history: { from: string; to: string; changedBy: string; note: string; changedAtUtc: string }[];
};

export default function CommitmentsPage() {
  const [items, setItems] = useState<Commitment[]>([]);
  const [error, setError] = useState("");

  async function load() {
    setItems(await api.get<Commitment[]>("/api/commitments"));
  }

  useEffect(() => { load().catch((e) => setError(errMessage(e))); }, []);

  // Deep link from the publish results panel: /commitments#<commitmentId>
  const [focusId, setFocusId] = useState("");
  useEffect(() => {
    if (items.length === 0) return;
    const hash = window.location.hash.slice(1);
    if (!hash) return;
    setFocusId(hash);
    document.getElementById(hash)?.scrollIntoView({ block: "center" });
  }, [items.length]);

  async function setStatus(id: string, status: string) {
    setError("");
    try {
      await api.post(`/api/commitments/${id}/status`, { status, note: `Set to ${status}` });
      await load();
    } catch (e: unknown) {
      setError(`Could not update status: ${errMessage(e)}`);
    }
  }

  return (
    <div className="stack">
      <PageHeader
        title="Commitments and Tasks"
        lede="Follow-up view with owners, due dates, destination task references, and status history. Overdue is computed from due dates."
      />
      {error && <div className="error">{error}</div>}
      {items.length === 0 ? (
        <div className="empty">No commitments yet. Publish an approved meeting draft to create them.</div>
      ) : (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>Action</th><th>Owner</th><th>Due</th><th>Status</th><th>Meeting</th><th>Task</th><th>History</th><th></th>
              </tr>
            </thead>
            <tbody>
              {items.map((c) => (
                <tr key={c.id} id={c.id} style={c.id === focusId ? { outline: "2px solid #2563eb", outlineOffset: -2 } : undefined}>
                  <td><strong>{c.action}</strong></td>
                  <td>{c.ownerName}</td>
                  <td>{c.dueDate ? new Date(c.dueDate).toLocaleDateString() : "—"}</td>
                  <td>
                    <span className={`badge ${c.status === "Overdue" ? "needs" : c.status === "Completed" ? "approved" : c.status === "InProgress" ? "sim" : "neutral"}`}>
                      {c.status.replace(/([a-z])([A-Z])/g, "$1 $2")}
                    </span>
                  </td>
                  <td>{c.meetingTitle || "—"}</td>
                  <td>
                    {c.task ? (
                      <>
                        <div><code>{c.task.externalTaskId || "—"}</code></div>
                        <div className="muted" style={{ fontSize: "0.8rem" }}>{c.task.destinationName} · {c.task.status}</div>
                        {c.task.failureReason && <div className="badge rejected">{c.task.failureReason}</div>}
                        <div className="badge sim">Simulated</div>
                      </>
                    ) : "—"}
                  </td>
                  <td>
                    <ul style={{ margin: 0, paddingLeft: "1rem", fontSize: "0.8rem" }}>
                      {c.history?.slice(-3).map((h, idx) => (
                        <li key={idx}>{h.from} → {h.to} ({h.changedBy})</li>
                      ))}
                    </ul>
                  </td>
                  <td className="form-actions" style={{ marginTop: 0 }}>
                    <button onClick={() => setStatus(c.id, "InProgress")}>In progress</button>
                    <button className="primary" onClick={() => setStatus(c.id, "Completed")}>Complete</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
