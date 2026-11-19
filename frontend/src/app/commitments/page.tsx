"use client";

import { useEffect, useState } from "react";
import { api, errMessage } from "@/lib/api";

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

  async function setStatus(id: string, status: string) {
    try {
      await api.post(`/api/commitments/${id}/status`, { status, note: `Set to ${status}` });
      await load();
    } catch (e: unknown) { setError(errMessage(e)); }
  }

  return (
    <div className="stack">
      <div>
        <h1 style={{ margin: 0 }}>Commitments and Tasks</h1>
        <p className="muted">Follow-up view. Overdue is computed from due dates. Task destination is simulated locally.</p>
      </div>
      {error && <div className="error">{error}</div>}
      <table className="table">
        <thead>
          <tr>
            <th>Action</th><th>Owner</th><th>Due</th><th>Status</th><th>Meeting</th><th>Task</th><th>History</th><th></th>
          </tr>
        </thead>
        <tbody>
          {items.map((c) => (
            <tr key={c.id}>
              <td>{c.action}</td>
              <td>{c.ownerName}</td>
              <td>{c.dueDate ? new Date(c.dueDate).toLocaleDateString() : "—"}</td>
              <td>
                <span className={`badge ${c.status === "Overdue" ? "needs" : c.status === "Completed" ? "approved" : ""}`}>
                  {c.status}
                </span>
              </td>
              <td>{c.meetingTitle || "—"}</td>
              <td>
                {c.task ? (
                  <>
                    <div>{c.task.externalTaskId || "—"}</div>
                    <div className="muted">{c.task.destinationName} · {c.task.status}</div>
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
              <td className="form-actions">
                <button onClick={() => setStatus(c.id, "InProgress")}>In progress</button>
                <button onClick={() => setStatus(c.id, "Completed")}>Complete</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
