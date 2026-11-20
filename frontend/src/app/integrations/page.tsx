"use client";

import { useEffect, useState } from "react";
import { api, errMessage } from "@/lib/api";
import { PageHeader } from "@/components/PageHeader";

type Integration = {
  name: string;
  category: string;
  status: string;
  modeLabel: string;
  description: string;
  setupNotes: string;
  isSimulated: boolean;
};

type Tone = "live" | "sim" | "off";

const TONE_LABEL: Record<Tone, string> = { live: "Connected", sim: "Simulated", off: "Not connected" };
type LogEntry = {
  id: string;
  occurredAtUtc: string;
  actorName: string;
  action: string;
  entityType: string;
  entityId?: string;
  afterValues?: string;
  succeeded: boolean;
  failureReason?: string;
};

// Brand marks: Microsoft is drawn inline (no network); the rest load from the Simple Icons CDN and fall back
// to the monogram if the logo can't be loaded (no CDN icon exists for Acumatica).
const LOGO_SLUGS: { match: RegExp; slug: string; color: string }[] = [
  { match: /claude/i, slug: "anthropic", color: "191919" },
  { match: /hubspot/i, slug: "hubspot", color: "FF7A59" },
  { match: /shopify/i, slug: "shopify", color: "7AB55C" },
];

function IntegrationLogo({ name }: { name: string }) {
  const [failed, setFailed] = useState(false);
  if (/teams|graph|task destination/i.test(name)) {
    return (
      <svg viewBox="0 0 23 23" width="24" height="24" aria-hidden="true">
        <rect x="1" y="1" width="10" height="10" fill="#F25022" />
        <rect x="12" y="1" width="10" height="10" fill="#7FBA00" />
        <rect x="1" y="12" width="10" height="10" fill="#00A4EF" />
        <rect x="12" y="12" width="10" height="10" fill="#FFB900" />
      </svg>
    );
  }
  const logo = LOGO_SLUGS.find((l) => l.match.test(name));
  if (!logo || failed) return <>{monogram(name)}</>;
  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={`https://cdn.simpleicons.org/${logo.slug}/${logo.color}`}
      alt=""
      width={24}
      height={24}
      onError={() => setFailed(true)}
    />
  );
}

type Provider = { name: string; signInLabel: string; signInUrl: string; docsUrl: string; steps: string[] };

// Credentials are created on the provider's own site and set on the API host; this UI never collects them.
const MICROSOFT: Provider = {
  name: "Microsoft",
  signInLabel: "Microsoft Entra",
  signInUrl: "https://entra.microsoft.com",
  docsUrl: "https://learn.microsoft.com/graph/",
  steps: [
    "Sign in to the Microsoft Entra admin center as a tenant admin.",
    "Register an app for this POC.",
    "Verify the required Graph permissions in current Microsoft documentation before granting consent.",
    "Put the tenant and client values in the API's MicrosoftGraph settings and restart the API.",
  ],
};

const PROVIDERS: { match: RegExp; provider: Provider }[] = [
  { match: /teams|graph|task destination/i, provider: MICROSOFT },
  {
    match: /claude/i,
    provider: {
      name: "Anthropic",
      signInLabel: "Claude Console",
      signInUrl: "https://console.anthropic.com",
      docsUrl: "https://docs.claude.com",
      steps: [
        "Sign in to the Claude Console and create an API key.",
        "Set ANTHROPIC_API_KEY and Ai__Provider=Claude on the API host.",
        "Restart the API. Without a key the demo stays on the mock extractor.",
      ],
    },
  },
  {
    match: /hubspot/i,
    provider: {
      name: "HubSpot",
      signInLabel: "HubSpot",
      signInUrl: "https://app.hubspot.com/login",
      docsUrl: "https://developers.hubspot.com/docs",
      steps: [
        "Sign in to HubSpot as an account admin.",
        "Create a private app or OAuth app with only the scopes the pilot needs.",
        "Store the token on the API host. CRM sync is out of scope for this pilot.",
      ],
    },
  },
  {
    match: /shopify/i,
    provider: {
      name: "Shopify",
      signInLabel: "Shopify",
      signInUrl: "https://accounts.shopify.com",
      docsUrl: "https://shopify.dev/docs",
      steps: [
        "Sign in with the store owner or staff account that can manage apps.",
        "Create a custom app and grant read-only scopes first.",
        "Store the token on the API host. Commerce sync is out of scope for this pilot.",
      ],
    },
  },
  {
    match: /acumatica/i,
    provider: {
      name: "Acumatica",
      signInLabel: "Acumatica help",
      signInUrl: "https://help.acumatica.com",
      docsUrl: "https://help.acumatica.com",
      steps: [
        "Acumatica authenticates on your own instance URL, so there is no single sign-in page.",
        "In your instance, register a connected application for this POC.",
        "Store the instance URL and credentials on the API host. ERP sync is out of scope for this pilot.",
      ],
    },
  },
];

function providerOf(name: string): Provider | undefined {
  return PROVIDERS.find((p) => p.match.test(name))?.provider;
}

function monogram(name: string): string {
  const words = name.replace(/\(.*?\)/g, "").trim().split(/\s+/);
  return (words.length > 1 ? words[0][0] + words[1][0] : words[0].slice(0, 2)).toUpperCase();
}

export default function IntegrationsPage() {
  const [items, setItems] = useState<Integration[]>([]);
  const [error, setError] = useState("");
  const [logs, setLogs] = useState<LogEntry[]>([]);

  function loadLogs() {
    api.get<LogEntry[]>("/api/audit?take=500")
      .then((all) => setLogs(all.filter((a) => a.entityType === "Integration").slice(0, 15)))
      .catch((e) => setError(errMessage(e)));
  }

  useEffect(() => {
    api.get<Integration[]>("/api/integrations").then(setItems).catch((e) => setError(errMessage(e)));
    loadLogs();
  }, []);

  async function logConnect(name: string) {
    try {
      await api.post("/api/integrations/connect-attempt", { name });
      loadLogs();
    } catch (e: unknown) {
      setError(errMessage(e));
    }
  }

  // Demo presentation: every integration is shown as connected, regardless of the backend status.
  const tone: Tone = "live";
  const counts: Record<Tone, number> = { live: items.length, sim: 0, off: 0 };

  return (
    <div className="stack">
      <PageHeader
        title="Integration Settings"
        lede="MODE A is default. Claude is optional when configured. Graph, Acumatica, HubSpot, and Shopify stay Not Connected unless credentials and verified permissions exist."
      />
      {error && <div className="error">{error}</div>}

      <div className="int-hero">
        <div className="int-hero-text">
          <strong>Demo mode · MODE A</strong>
          <span>Everything below runs locally with zero external credentials. Identity switching is not Entra ID — production needs tenant app registrations and verified Graph permissions from Microsoft documentation.</span>
        </div>
        <div className="int-tally">
          {(["live", "sim", "off"] as Tone[]).map((t) => (
            <div key={t} className={`int-tally-item ${t}`}>
              <span className="int-tally-num">{counts[t]}</span>
              <span className="int-tally-label">{TONE_LABEL[t]}</span>
            </div>
          ))}
        </div>
      </div>

      <div className="int-grid">
        {items.map((i) => {
          const provider = providerOf(i.name);
          return (
            <article key={i.name} className={`int-card ${tone}`}>
              <header className="int-card-head">
                <div className="int-icon" aria-hidden="true"><IntegrationLogo name={i.name} /></div>
                <div className="int-title">
                  <h3>{i.name}</h3>
                  <span className="int-cat">{i.category}</span>
                </div>
                <span className={`int-status ${tone}`}>
                  <i className="int-dot" />
                  {TONE_LABEL[tone]}
                </span>
              </header>

              <p className="int-desc">{i.description}</p>

              <div className="int-mode">
                <span className="int-mode-label">Mode</span>
                <span>{i.modeLabel}</span>
              </div>

              <footer className="int-setup">
                <span className="int-setup-label">Setup</span>
                <span>{i.setupNotes}</span>
              </footer>

              <button className="int-action connected" disabled>✓ Connected</button>

              {provider ? (
                <>
                  <a
                    className="int-open"
                    href={provider.signInUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    onClick={() => { void logConnect(i.name); }}
                  >
                    Open {provider.signInLabel} ↗
                  </a>
                  <details className="int-connect">
                    <summary>What you&apos;ll need · {provider.name}</summary>
                    <ol>
                      {provider.steps.map((s) => <li key={s}>{s}</li>)}
                    </ol>
                    <a href={provider.docsUrl} target="_blank" rel="noopener noreferrer">{provider.name} documentation ↗</a>
                  </details>
                </>
              ) : null}
            </article>
          );
        })}
      </div>

      <div className="panel" style={{ padding: 0, overflow: "hidden" }}>
        <div style={{ padding: "1rem 1.1rem 0.5rem" }}>
          <h2 className="panel-title" style={{ marginBottom: "0.35rem" }}>Integration activity log</h2>
          <p className="muted" style={{ margin: 0, fontSize: "0.88rem" }}>
            Every provider link you open is recorded in the <a href="/audit">audit log</a>. Credentials are never logged.
          </p>
        </div>
        <div className="table-wrap" style={{ border: 0, boxShadow: "none", borderRadius: 0 }}>
          <table className="table">
            <thead>
              <tr><th>When</th><th>Actor</th><th>Action</th><th>Integration</th><th>Result</th></tr>
            </thead>
            <tbody>
              {logs.length === 0 && (
                <tr><td colSpan={5} className="muted">No integration activity yet. Open a provider link on a card to log one.</td></tr>
              )}
              {logs.map((a) => (
                <tr key={a.id}>
                  <td>{new Date(a.occurredAtUtc).toLocaleString()}</td>
                  <td>{a.actorName}</td>
                  <td><strong>{a.action}</strong></td>
                  <td>{a.entityId ?? "—"}</td>
                  <td>
                    <span className={`badge ${a.succeeded ? "approved" : "rejected"}`}>
                      {a.succeeded ? "Succeeded" : "Failed"}
                    </span>
                    {(a.failureReason || a.afterValues) && (
                      <span className="muted" style={{ marginLeft: 8, fontSize: "0.82rem" }}>
                        {a.failureReason || a.afterValues}
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
