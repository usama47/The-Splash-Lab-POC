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

// Sample figures for demonstration only — not client data.
const SAMPLE = { invoicesPerMonth: 120, minutesPerInvoice: 8, hourlyRate: 35 };
const SAMPLE_HOURS = (SAMPLE.invoicesPerMonth * SAMPLE.minutesPerInvoice) / 60;
const SAMPLE_COST = SAMPLE_HOURS * SAMPLE.hourlyRate;

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
      <div className="panel stack">
        <div className="row" style={{ justifyContent: "space-between" }}>
          <h2 className="panel-title" style={{ margin: 0 }}>Worked example: Acumatica invoice entry — time and cost</h2>
          <span className="badge sim">Sample figures — not client data</span>
        </div>
        <div className="grid-cards">
          <div className="stat">
            <div className="label">Volume (sample)</div>
            <div className="value">{SAMPLE.invoicesPerMonth}</div>
            <div className="muted" style={{ fontSize: "0.8rem", marginTop: 4 }}>Acumatica invoices per month</div>
          </div>
          <div className="stat">
            <div className="label">Handling time (sample)</div>
            <div className="value">{SAMPLE.minutesPerInvoice} min</div>
            <div className="muted" style={{ fontSize: "0.8rem", marginTop: 4 }}>per invoice</div>
          </div>
          <div className="stat">
            <div className="label">Time per month</div>
            <div className="value">{SAMPLE_HOURS} h</div>
            <div className="muted" style={{ fontSize: "0.8rem", marginTop: 4 }}>
              {SAMPLE.invoicesPerMonth} × {SAMPLE.minutesPerInvoice} min = {SAMPLE.invoicesPerMonth * SAMPLE.minutesPerInvoice} min
            </div>
          </div>
          <div className="stat">
            <div className="label">Monthly labor cost (sample rate)</div>
            <div className="value">${SAMPLE_COST}</div>
            <div className="muted" style={{ fontSize: "0.8rem", marginTop: 4 }}>
              {SAMPLE_HOURS} h × ${SAMPLE.hourlyRate}/h sample hourly rate
            </div>
          </div>
        </div>
        <p className="muted" style={{ margin: 0 }}>
          Illustrative arithmetic only: replace the sample volume, minutes and hourly rate with measured values from the
          client before using these numbers in a business case.
        </p>
      </div>
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
