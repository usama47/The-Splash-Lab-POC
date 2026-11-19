"use client";

import { useEffect, useState } from "react";
import { api, errMessage } from "@/lib/api";

type Decision = {
  id: string;
  statement: string;
  context: string;
  meetingTitle?: string;
  approverName: string;
  decisionDateUtc: string;
  isApproved: boolean;
  department: string;
};

export default function DecisionsPage() {
  const [items, setItems] = useState<Decision[]>([]);
  const [error, setError] = useState("");

  useEffect(() => {
    api.get<Decision[]>("/api/decisions").then(setItems).catch((e) => setError(errMessage(e)));
  }, []);

  return (
    <div className="stack">
      <div>
        <h1 style={{ margin: 0 }}>Decisions Register</h1>
        <p className="muted">Only human-approved decisions appear here.</p>
      </div>
      {error && <div className="error">{error}</div>}
      <table className="table">
        <thead>
          <tr><th>Decision</th><th>Context</th><th>Meeting</th><th>Approver</th><th>Date</th><th>Status</th></tr>
        </thead>
        <tbody>
          {items.map((d) => (
            <tr key={d.id}>
              <td>{d.statement}</td>
              <td>{d.context}</td>
              <td>{d.meetingTitle || "—"}</td>
              <td>{d.approverName}</td>
              <td>{new Date(d.decisionDateUtc).toLocaleString()}</td>
              <td><span className="badge approved">{d.isApproved ? "Approved" : "Draft"}</span></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
