"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Dashboard, api, errMessage } from "@/lib/api";

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
      <div>
        <h1 style={{ margin: 0 }}>Dashboard</h1>
        <p className="muted">Operations pilot — meeting-to-task foundation (persisted SQLite data).</p>
      </div>
      <div className="notice">
        External systems are simulated in MODE A. Start with{" "}
        <Link href="/meetings">Meetings</Link> → Generate Draft → Review → Publish.
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
        <h2 style={{ marginTop: 0, fontSize: "1.05rem" }}>Meeting-to-task workflow progress</h2>
        <div className="progress">
          {data.workflowProgress.map((s) => (
            <div className="step" key={s.step}>
              <span className="muted">{s.step}</span>
              <strong>{s.count}</strong>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
