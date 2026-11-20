"use client";

import { useEffect, useState } from "react";
import { api, errMessage } from "@/lib/api";
import { PageHeader } from "@/components/PageHeader";
import { toast } from "@/components/Toaster";

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

  async function approve(id: string, approveFlag: boolean) {
    try {
      await api.post(`/api/knowledge/${id}/approve`, { approve: approveFlag });
      toast(approveFlag ? "Approved successfully." : "Rejected successfully.");
      await load();
    } catch (e: unknown) { setError(errMessage(e)); }
  }

  return (
    <div className="stack">
      <PageHeader
        title="Knowledge Library"
        lede="Approved process documentation and reference notes. Drafts do not become approved automatically."
      />
      {error && <div className="error">{error}</div>}
      <div className="panel">
        <div className="row">
          <input
            style={{ flex: 1, minWidth: 180, border: "1px solid var(--border)", borderRadius: 7, padding: "0.55rem 0.65rem" }}
            placeholder="Search title, description, department…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <label className="row" style={{ gap: "0.4rem" }}>
            <input type="checkbox" checked={approvedOnly} onChange={(e) => setApprovedOnly(e.target.checked)} />
            Approved only
          </label>
          <button className="primary" onClick={() => load()}>Search</button>
        </div>
      </div>
      <div className="table-wrap">
        <table className="table">
          <thead>
            <tr><th>Title</th><th>Department</th><th>Owner</th><th>Status</th><th>Description</th><th></th></tr>
          </thead>
          <tbody>
            {items.map((k) => (
              <tr key={k.id}>
                <td><strong>{k.title}</strong></td>
                <td>{k.department}</td>
                <td>{k.ownerName}</td>
                <td>
                  <span className={`badge ${k.approvalStatus === "Approved" ? "approved" : "needs"}`}>
                    {k.approvalStatus.replace(/([a-z])([A-Z])/g, "$1 $2")}
                  </span>
                </td>
                <td>{k.description}</td>
                <td>
                  {k.approvalStatus !== "Approved" && (
                    <div className="form-actions" style={{ marginTop: 0 }}>
                      <button className="primary" onClick={() => approve(k.id, true)}>Approve</button>
                      <button onClick={() => approve(k.id, false)}>Reject</button>
                    </div>
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
