"use client";

import { useEffect, useState } from "react";
import { api, errMessage } from "@/lib/api";

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
      <div>
        <h1 style={{ margin: 0 }}>Process Register</h1>
        <p className="muted">Baseline mapping for the 50% admin-work reduction ambition.</p>
      </div>
      {error && <div className="error">{error}</div>}
      <div className="grid-cards">
        {summary.map((s) => (
          <div className="stat" key={s.classification}>
            <div className="label">{s.classification} ({s.processCount})</div>
            <div className="value">{s.totalHoursPerWeek}h</div>
            <div className="muted">est. weekly hours</div>
          </div>
        ))}
      </div>
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
              <td>{p.processName}</td>
              <td>{p.department}</td>
              <td>{p.region}</td>
              <td>{p.ownerName}</td>
              <td>{p.frequency}</td>
              <td>{p.estimatedHoursPerWeek}</td>
              <td>{p.systemsUsed}</td>
              <td>{p.painScore}</td>
              <td>{p.classification}</td>
              <td>{p.opportunityForImprovement}</td>
              <td>{p.currentStatus}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
