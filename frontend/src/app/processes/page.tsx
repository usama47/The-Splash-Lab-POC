"use client";

import { useEffect, useState } from "react";
import { api, errMessage } from "@/lib/api";
import { PageHeader } from "@/components/PageHeader";

type Process = {
  id: string;
  processName: string;
  department: string;
  region: string;
  ownerName: string;
  frequency: string;
  estimatedHoursPerWeek: number;
  systemsUsed: string;
  painScore: number;
  classification: string;
  opportunityForImprovement: string;
  currentStatus: string;
};

type Summary = { classification: string; totalHoursPerWeek: number; processCount: number };

export default function ProcessesPage() {
  const [items, setItems] = useState<Process[]>([]);
  const [summary, setSummary] = useState<Summary[]>([]);
  const [error, setError] = useState("");

  useEffect(() => {
    Promise.all([
      api.get<Process[]>("/api/processes"),
      api.get<Summary[]>("/api/processes/summary"),
    ]).then(([p, s]) => { setItems(p); setSummary(s); })
      .catch((e) => setError(errMessage(e)));
  }, []);

  return (
    <div className="stack">
      <PageHeader
        title="Process Register"
        lede="Baseline mapping for the ambition to cut routine admin work by 50% — hours by Automate / Accelerate / Elevate."
      />
      {error && <div className="error">{error}</div>}
      <div className="grid-cards">
        {summary.map((s) => (
          <div className="stat" key={s.classification}>
            <div className="label">{s.classification} · {s.processCount} processes</div>
            <div className="value">{s.totalHoursPerWeek}h</div>
            <div className="muted" style={{ fontSize: "0.8rem", marginTop: 4 }}>est. weekly hours</div>
          </div>
        ))}
      </div>
      <div className="table-wrap">
        <table className="table">
          <thead>
            <tr>
              <th>Process</th><th>Dept</th><th>Region</th><th>Owner</th><th>Frequency</th>
              <th>Hours/wk</th><th>Systems</th><th>Pain</th><th>Class</th><th>Opportunity</th><th>Status</th>
            </tr>
          </thead>
          <tbody>
            {items.map((p) => (
              <tr key={p.id}>
                <td><strong>{p.processName}</strong></td>
                <td>{p.department}</td>
                <td>{p.region}</td>
                <td>{p.ownerName}</td>
                <td>{p.frequency}</td>
                <td>{p.estimatedHoursPerWeek}</td>
                <td>{p.systemsUsed}</td>
                <td><span className={`badge ${p.painScore >= 4 ? "needs" : "neutral"}`}>{p.painScore}/5</span></td>
                <td><span className="badge sim">{p.classification}</span></td>
                <td>{p.opportunityForImprovement}</td>
                <td>{p.currentStatus}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
