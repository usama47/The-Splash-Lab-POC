"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { MeetingSummary, api, errMessage } from "@/lib/api";

export default function ReviewQueuePage() {
  const [items, setItems] = useState<MeetingSummary[]>([]);
  const [error, setError] = useState("");

  useEffect(() => {
    api.get<MeetingSummary[]>("/api/meetings")
      .then((all) => setItems(all.filter((m) => ["DraftGenerated", "InReview", "PartiallyPublished"].includes(m.workflowStatus))))
      .catch((e) => setError(errMessage(e)));
  }, []);

  return (
    <div className="stack">
      <div>
        <h1 style={{ margin: 0 }}>Review Queue</h1>
        <p className="muted">Drafts awaiting reviewer approval before they enter official registers.</p>
      </div>
      {error && <div className="error">{error}</div>}
      <table className="table">
        <thead>
          <tr><th>Meeting</th><th>Status</th><th>Scenario</th><th></th></tr>
        </thead>
        <tbody>
          {items.length === 0 && (
            <tr><td colSpan={4} className="muted">No drafts waiting. Generate a draft from Meetings.</td></tr>
          )}
          {items.map((m) => (
            <tr key={m.id}>
              <td>{m.title}</td>
              <td>{m.workflowStatus}</td>
              <td>{m.scenarioKey}</td>
              <td><Link href={`/meetings/${m.id}`}>Review</Link></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
