"use client";

import { useEffect, useState } from "react";
import { api, errMessage } from "@/lib/api";
import { PageHeader } from "@/components/PageHeader";

type Issue = {
  id: string;
  title: string;
  description: string;
  department: string;
  ownerName: string;
  status: string;
  priority: string;
  resolution?: string;
  knowledgeArticleId?: string;
};

export default function IssuesPage() {
  const [items, setItems] = useState<Issue[]>([]);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  async function load() {
    setItems(await api.get<Issue[]>("/api/issues"));
  }

  useEffect(() => { load().catch((e) => setError(errMessage(e))); }, []);

  async function publishKnowledge(issue: Issue) {
    try {
      const article = await api.post<{ id: string; approvalStatus: string }>(`/api/issues/${issue.id}/publish-knowledge`, {
        title: `Knowledge: ${issue.title}`,
        description: issue.resolution || issue.description,
      });
      setMessage(`Knowledge draft created (${article.approvalStatus}). Approve it under Knowledge.`);
      await load();
    } catch (e: unknown) { setError(errMessage(e)); }
  }

  return (
    <div className="stack">
      <PageHeader
        title="Issues Register"
        lede="Track open and resolved issues. Resolved items can be proposed as knowledge — approval is still required."
      />
      {error && <div className="error">{error}</div>}
      {message && <div className="notice">{message}</div>}
      <div className="table-wrap">
        <table className="table">
          <thead>
            <tr><th>Title</th><th>Department</th><th>Owner</th><th>Status</th><th>Priority</th><th>Resolution</th><th></th></tr>
          </thead>
          <tbody>
            {items.map((i) => (
              <tr key={i.id}>
                <td>
                  <strong>{i.title}</strong>
                  <div className="muted" style={{ fontSize: "0.82rem", marginTop: 4 }}>{i.description}</div>
                </td>
                <td>{i.department}</td>
                <td>{i.ownerName}</td>
                <td>
                  <span className={`badge ${i.status === "Resolved" || i.status === "Closed" ? "approved" : i.status === "Open" ? "needs" : "sim"}`}>
                    {i.status}
                  </span>
                </td>
                <td>
                  <span className={`badge ${i.priority === "Critical" || i.priority === "High" ? "rejected" : "neutral"}`}>
                    {i.priority}
                  </span>
                </td>
                <td>{i.resolution || "—"}</td>
                <td>
                  {(i.status === "Resolved" || i.status === "Closed") && !i.knowledgeArticleId && (
                    <button onClick={() => publishKnowledge(i)}>Propose knowledge</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
