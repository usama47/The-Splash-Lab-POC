"use client";

import { useEffect, useState } from "react";
import { api, errMessage } from "@/lib/api";
import { PageHeader } from "@/components/PageHeader";

type Audit = {
  id: string;
  occurredAtUtc: string;
  actorName: string;
  action: string;
  entityType: string;
  entityId?: string;
  meetingId?: string;
  beforeValues?: string;
  afterValues?: string;
  succeeded: boolean;
  failureReason?: string;
};

export default function AuditPage() {
  const [items, setItems] = useState<Audit[]>([]);
  const [error, setError] = useState("");

  useEffect(() => {
    api.get<Audit[]>("/api/audit").then(setItems).catch((e) => setError(errMessage(e)));
  }, []);

  return (
    <div className="stack">
      <PageHeader
        title="Audit Log"
        lede="Append-only history of who did what, when, and whether it succeeded. Credentials and tokens are never logged."
      />
      {error && <div className="error">{error}</div>}
      <div className="table-wrap">
        <table className="table">
          <thead>
            <tr>
              <th>When (UTC)</th><th>Actor</th><th>Action</th><th>Entity</th><th>Meeting</th><th>Result</th><th>Details</th>
            </tr>
          </thead>
          <tbody>
            {items.map((a) => (
              <tr key={a.id}>
                <td>{new Date(a.occurredAtUtc).toLocaleString()}</td>
                <td>{a.actorName}</td>
                <td><strong>{a.action}</strong></td>
                <td>{a.entityType}{a.entityId ? ` · ${a.entityId.slice(0, 8)}` : ""}</td>
                <td>{a.meetingId ? a.meetingId.slice(0, 8) : "—"}</td>
                <td>
                  <span className={`badge ${a.succeeded ? "approved" : "rejected"}`}>
                    {a.succeeded ? "Succeeded" : "Failed"}
                  </span>
                </td>
                <td style={{ maxWidth: 340, fontSize: "0.82rem" }}>
                  {a.failureReason || a.afterValues || a.beforeValues || "—"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
