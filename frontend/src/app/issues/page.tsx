"use client";

import { useEffect, useState } from "react";
import { api, errMessage } from "@/lib/api";

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
      <div>
        <h1 style={{ margin: 0 }}>Issues Register</h1>
        <p className="muted">Resolved issues can be proposed as knowledge — approval still required.</p>
      </div>
      {error && <div className="error">{error}</div>}
      {message && <div className="notice">{message}</div>}
      <table className="table">
        <thead>
          <tr><th>Title</th><th>Department</th><th>Owner</th><th>Status</th><th>Priority</th><th>Resolution</th><th></th></tr>
        </thead>
        <tbody>
          {items.map((i) => (
            <tr key={i.id}>
              <td>{i.title}</td>
              <td>{i.department}</td>
              <td>{i.ownerName}</td>
              <td>{i.status}</td>
              <td>{i.priority}</td>
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
  );
}
