"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { MeetingSummary, api, errMessage } from "@/lib/api";
import { PageHeader } from "@/components/PageHeader";

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
      <PageHeader
        title="Review Queue"
        lede="Drafts waiting for reviewer approval before they enter official registers or the task destination."
      />
      {error && <div className="error">{error}</div>}
      {items.length === 0 ? (
        <div className="empty">
          No drafts waiting. Generate a draft from <Link href="/meetings">Meetings</Link> to populate this queue.
        </div>
      ) : (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr><th>Meeting</th><th>Status</th><th>Scenario</th><th></th></tr>
            </thead>
            <tbody>
              {items.map((m) => (
                <tr key={m.id}>
                  <td><strong>{m.title}</strong></td>
                  <td><span className="badge needs">{m.workflowStatus.replace(/([a-z])([A-Z])/g, "$1 $2")}</span></td>
                  <td><span className="badge neutral">{m.scenarioKey}</span></td>
                  <td><Link className="link-btn" href={`/meetings/${m.id}`}>Review →</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
