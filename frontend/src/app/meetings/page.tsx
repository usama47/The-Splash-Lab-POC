"use client";

import Link from "next/link";
import { FormEvent, useEffect, useState } from "react";
import { MeetingSummary, api, errMessage } from "@/lib/api";

export default function MeetingsPage() {
  const [meetings, setMeetings] = useState<MeetingSummary[]>([]);
  const [error, setError] = useState("");
  const [title, setTitle] = useState("");
  const [organizer, setOrganizer] = useState("Alex Morgan");
  const [transcript, setTranscript] = useState("");
  const [file, setFile] = useState<File | null>(null);

  async function load() {
    try {
      setMeetings(await api.get<MeetingSummary[]>("/api/meetings?department=Operations"));
    } catch (e: unknown) {
      setError(errMessage(e));
    }
  }

  useEffect(() => { load(); }, []);

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
      <div>
        <h1 style={{ margin: 0 }}>Meetings</h1>
        <p className="muted">Pilot department: Operations. Source transcripts are simulated Teams content unless uploaded.</p>
      </div>
      {error && <div className="error">{error}</div>}
      <div className="panel">
        <h2 style={{ marginTop: 0, fontSize: "1rem" }}>Sample / uploaded meetings</h2>
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
                <td>{m.title}</td>
                <td>{new Date(m.meetingDate).toLocaleDateString()}</td>
                <td><span className="badge">{m.scenarioKey || "uploaded"}</span></td>
                <td>{m.source}</td>
                <td>{m.workflowStatus}</td>
                <td><Link href={`/meetings/${m.id}`}>Open</Link></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="panel">
        <h2 style={{ marginTop: 0, fontSize: "1rem" }}>Paste transcript</h2>
        <form onSubmit={onPaste}>
          <div className="form-row">
            <label>Title</label>
            <input value={title} onChange={(e) => setTitle(e.target.value)} />
          </div>
          <div className="form-row">
            <label>Organizer</label>
            <input value={organizer} onChange={(e) => setOrganizer(e.target.value)} />
          </div>
          <div className="form-row">
            <label>Transcript</label>
            <textarea rows={8} value={transcript} onChange={(e) => setTranscript(e.target.value)} required minLength={20} />
          </div>
          <button className="primary" type="submit">Create meeting</button>
        </form>
      </div>
      <div className="panel">
        <h2 style={{ marginTop: 0, fontSize: "1rem" }}>Upload .txt transcript</h2>
        <form onSubmit={onUpload}>
          <div className="form-row">
            <label>File</label>
            <input type="file" accept=".txt,text/plain" onChange={(e) => setFile(e.target.files?.[0] || null)} />
          </div>
          <button className="primary" type="submit">Upload</button>
        </form>
      </div>
    </div>
  );
}
