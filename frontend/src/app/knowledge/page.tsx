"use client";

import { useEffect, useState } from "react";
import { api, errMessage } from "@/lib/api";

type Article = {
  id: string;
  title: string;
  description: string;
  department: string;
  ownerName: string;
  approvalStatus: string;
  approvedByName?: string;
  createdAtUtc: string;
};

export default function KnowledgePage() {
  const [items, setItems] = useState<Article[]>([]);
  const [search, setSearch] = useState("");
  const [approvedOnly, setApprovedOnly] = useState(false);
  const [error, setError] = useState("");

  async function load(q = search, only = approvedOnly) {
    const qs = new URLSearchParams();
    if (q) qs.set("search", q);
    if (only) qs.set("approvedOnly", "true");
    setItems(await api.get<Article[]>(`/api/knowledge?${qs}`));
  }

  useEffect(() => { load().catch((e) => setError(errMessage(e))); }, []);

  async function approve(id: string, approve: boolean) {
    try {
      await api.post(`/api/knowledge/${id}/approve`, { approve });
      await load();
    } catch (e: unknown) { setError(errMessage(e)); }
  }

  return (
    <div className="stack">
      <div>
        <h1 style={{ margin: 0 }}>Knowledge Library</h1>
        <p className="muted">Draft knowledge does not become approved automatically.</p>
      </div>
      {error && <div className="error">{error}</div>}
      <div className="row">
        <input placeholder="Search…" value={search} onChange={(e) => setSearch(e.target.value)} />
        <label className="row">
          <input type="checkbox" checked={approvedOnly} onChange={(e) => setApprovedOnly(e.target.checked)} />
          Approved only
        </label>
        <button onClick={() => load()}>Search</button>
      </div>
      <table className="table">
        <thead>
          <tr><th>Title</th><th>Department</th><th>Owner</th><th>Status</th><th>Description</th><th></th></tr>
        </thead>
        <tbody>
          {items.map((k) => (
            <tr key={k.id}>
              <td>{k.title}</td>
              <td>{k.department}</td>
              <td>{k.ownerName}</td>
              <td>
                <span className={`badge ${k.approvalStatus === "Approved" ? "approved" : "needs"}`}>
                  {k.approvalStatus}
                </span>
              </td>
              <td>{k.description}</td>
              <td>
                {k.approvalStatus !== "Approved" && (
                  <div className="form-actions">
                    <button onClick={() => approve(k.id, true)}>Approve</button>
                    <button onClick={() => approve(k.id, false)}>Reject</button>
                  </div>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
