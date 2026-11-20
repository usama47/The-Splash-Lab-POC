"use client";

import Link from "next/link";
import { FormEvent, useEffect, useState } from "react";
import { MeetingSummary, api, errMessage } from "@/lib/api";
import { PageHeader } from "@/components/PageHeader";

export default function MeetingsPage() {
  const [meetings, setMeetings] = useState<MeetingSummary[]>([]);
  const [error, setError] = useState("");
  const [title, setTitle] = useState("");
  const [organizer, setOrganizer] = useState("Alex Morgan");
  const [transcript, setTranscript] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);

  async function load() {
    try {
      setMeetings(await api.get<MeetingSummary[]>("/api/meetings?department=Operations"));
    } catch (e: unknown) {
      setError(errMessage(e));
    }
  }

  useEffect(() => { load(); }, []);

  async function startFresh() {
    if (!window.confirm("This deletes ALL data (including pasted/uploaded meetings, drafts, registers and the audit trail) and restores the original demo data. Continue?")) return;
    setError("");
    setBusy(true);
    try {
      await api.post("/api/meetings/demo-scenario");
      await load();
    } catch (err: unknown) {
      setError(errMessage(err));
    } finally {
      setBusy(false);
    }
  }

  async function onPaste(e: FormEvent) {
    e.preventDefault();
    setError("");
    try {
      await api.post("/api/meetings", {
        title: title || "Uploaded meeting",
        department: "Operations",
        organizer,
        meetingDate: new Date().toISOString(),
        transcript,
        source: "ManualPaste",
      });
      setTranscript("");
      setTitle("");
      await load();
    } catch (err: unknown) {
      setError(errMessage(err));
    }
  }

  async function onUpload(e: FormEvent) {
    e.preventDefault();
    if (!file) return setError("Choose a .txt file.");
    setError("");
    const form = new FormData();
    form.append("file", file);
    form.append("title", title || file.name);
    form.append("department", "Operations");
    form.append("organizer", organizer);
    try {
      await api.upload("/api/meetings/upload", form);
      setFile(null);
      await load();
    } catch (err: unknown) {
      setError(errMessage(err));
    }
  }

  return (
    <div className="stack">
      <PageHeader
        title="Meetings"
        lede="Pilot department: Operations. Use the three seeded scenarios for the live walkthrough, or paste/upload a transcript."
        actions={
          <button className="primary" disabled={busy} onClick={startFresh}>
            Start a fresh demo scenario
          </button>
        }
      />
      {error && <div className="error">{error}</div>}
      <div className="panel" style={{ padding: 0, overflow: "hidden" }}>
        <div style={{ padding: "1rem 1.1rem 0.5rem" }}>
          <h2 className="panel-title" style={{ marginBottom: "0.35rem" }}>Sample & uploaded meetings</h2>
          <p className="muted" style={{ margin: 0, fontSize: "0.88rem" }}>
            Scenario badges: <span className="badge sim">complete</span>{" "}
            <span className="badge needs">ambiguous</span>{" "}
            <span className="badge rejected">fail-destination</span>{" "}
            <span className="badge approved">prior-published</span>
          </p>
        </div>
        <div className="table-wrap" style={{ border: "none", borderRadius: 0, boxShadow: "none" }}>
          <table className="table">
            <thead>
              <tr>
                <th>Title</th>
                <th>Date</th>
                <th>Scenario</th>
                <th>Source</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {meetings.map((m) => (
                <tr key={m.id}>
                  <td><strong>{m.title}</strong></td>
                  <td>{new Date(m.meetingDate).toLocaleDateString()}</td>
                  <td><span className="badge neutral">{m.scenarioKey || "uploaded"}</span></td>
                  <td>{m.source}</td>
                  <td><span className="badge sim">{m.workflowStatus.replace(/([a-z])([A-Z])/g, "$1 $2")}</span></td>
                  <td><Link className="link-btn" href={`/meetings/${m.id}`}>Open →</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: "0.9rem" }}>
        <div className="panel">
          <h2 className="panel-title">Paste transcript</h2>
          <form onSubmit={onPaste}>
            <div className="form-row">
              <label>Title</label>
              <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Meeting title" />
            </div>
            <div className="form-row">
              <label>Organizer</label>
              <input value={organizer} onChange={(e) => setOrganizer(e.target.value)} />
            </div>
            <div className="form-row">
              <label>Transcript</label>
              <textarea rows={7} value={transcript} onChange={(e) => setTranscript(e.target.value)} required minLength={20} placeholder="Paste meeting notes…" />
            </div>
            <button className="primary" type="submit">Create meeting</button>
          </form>
        </div>
        <div className="panel">
          <h2 className="panel-title">Upload .txt transcript</h2>
          <form onSubmit={onUpload}>
            <div className="form-row">
              <label>File</label>
              <input type="file" accept=".txt,text/plain" onChange={(e) => setFile(e.target.files?.[0] || null)} />
            </div>
            <p className="muted" style={{ fontSize: "0.85rem" }}>Max 200KB. Empty files are rejected.</p>
            <button className="primary" type="submit">Upload</button>
          </form>
        </div>
      </div>
    </div>
  );
}
