"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Dashboard, api, errMessage } from "@/lib/api";
import { PageHeader } from "@/components/PageHeader";

export default function DashboardPage() {
  const [data, setData] = useState<Dashboard | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    api.get<Dashboard>("/api/dashboard")
      .then(setData)
      .catch((e) => setError(errMessage(e)));
  }, []);

  if (error) return <div className="error">API error: {error}. Is the backend running on :5080?</div>;
  if (!data) return <p className="muted">Loading dashboard…</p>;

  const stats = [
    { label: "Meetings processed", value: data.meetingsProcessed },
    { label: "Drafts awaiting approval", value: data.draftsAwaitingApproval },
    { label: "Approved decisions", value: data.approvedDecisions },
    { label: "Open commitments", value: data.openCommitments },
    { label: "Overdue commitments", value: data.overdueCommitments },
    { label: "Processes mapped", value: data.processesMapped },
  ];

  return (
    <div className="stack">
      <PageHeader
        title="Dashboard"
        lede="Operations pilot foundation — live counts from the local SQLite database."
        actions={
          <Link className="link-btn" href="/meetings">
            Start demo workflow →
          </Link>
        }
      />
      <div className="notice">
        External systems are simulated in MODE A. Client path:{" "}
        <Link href="/meetings">Meetings</Link> → UK sample meeting → Generate Draft → approve → Publish →
        Decisions, Commitments, Audit Log.
      </div>
      <div className="grid-cards">
        {stats.map((s) => (
          <div className="stat" key={s.label}>
            <div className="label">{s.label}</div>
            <div className="value">{s.value}</div>
          </div>
        ))}
      </div>
      <div className="panel">
        <h2 className="panel-title">Meeting-to-task workflow progress</h2>
        <div className="progress">
          {data.workflowProgress.map((s) => (
            <div className="step" key={s.step}>
              <span>{s.step}</span>
              <strong>{s.count}</strong>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
