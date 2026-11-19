"use client";

import { useEffect, useState } from "react";
import { api, errMessage } from "@/lib/api";

type Integration = {
  name: string;
  category: string;
  status: string;
  modeLabel: string;
  description: string;
  setupNotes: string;
  isSimulated: boolean;
};

export default function IntegrationsPage() {
  const [items, setItems] = useState<Integration[]>([]);
  const [error, setError] = useState("");

  useEffect(() => {
    api.get<Integration[]>("/api/integrations").then(setItems).catch((e) => setError(errMessage(e)));
  }, []);

  return (
    <div className="stack">
      <div>
        <h1 style={{ margin: 0 }}>Integration Settings</h1>
        <p className="muted">
          MODE A is default. Claude is optional when configured. Microsoft Graph / Acumatica / HubSpot / Shopify remain Not Connected unless credentials and verified permissions exist.
        </p>
      </div>
      {error && <div className="error">{error}</div>}
      <div className="notice">
        Demo identity switching is not Entra ID. Production will require proper tenant app registrations and verified Graph permissions from Microsoft documentation.
      </div>
      <table className="table">
        <thead>
          <tr><th>Integration</th><th>Category</th><th>Status</th><th>Mode</th><th>Description</th><th>Setup</th></tr>
        </thead>
        <tbody>
          {items.map((i) => (
            <tr key={i.name}>
              <td>{i.name}</td>
              <td>{i.category}</td>
              <td>
                <span className={`badge ${i.status === "NotConnected" ? "rejected" : i.isSimulated ? "sim" : "approved"}`}>
                  {i.status}
                </span>
              </td>
              <td>{i.modeLabel} {i.isSimulated && <span className="badge sim">Simulated</span>}</td>
              <td>{i.description}</td>
              <td style={{ fontSize: "0.85rem" }}>{i.setupNotes}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
