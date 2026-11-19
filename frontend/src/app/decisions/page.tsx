"use client";

import { useEffect, useState } from "react";
import { api, errMessage } from "@/lib/api";
import { PageHeader } from "@/components/PageHeader";

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
      <PageHeader title="Decisions Register" lede="Only human-approved decisions become official records." />
      {error && <div className="error">{error}</div>}
      {items.length === 0 ? (
        <div className="empty">No approved decisions yet.</div>
      ) : (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr><th>Decision</th><th>Context</th><th>Meeting</th><th>Approver</th><th>Date</th><th>Status</th></tr>
            </thead>
            <tbody>
              {items.map((d) => (
                <tr key={d.id}>
                  <td><strong>{d.statement}</strong></td>
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
      )}
    </div>
  );
}
