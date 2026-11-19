"use client";

import { useParams, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { DraftItem, MeetingDetail, api, errMessage } from "@/lib/api";
import { PageHeader } from "@/components/PageHeader";
import Link from "next/link";

type Editable = {
  summary: string;
  discussionPoints: string[];
  decisions: DraftItem[];
  commitments: DraftItem[];
  issues: DraftItem[];
};

export default function MeetingDetailPage() {
  const params = useParams();
  const id = params.id as string;
  const router = useRouter();
  const [meeting, setMeeting] = useState<MeetingDetail | null>(null);
  const [edit, setEdit] = useState<Editable | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);

  async function load() {
    const m = await api.get<MeetingDetail>(`/api/meetings/${id}`);
    setMeeting(m);
    if (m.draft) {
      setEdit({
        summary: m.draft.summary,
        discussionPoints: m.draft.discussionPoints || [],
        decisions: m.draft.decisions.map((d) => ({ ...d })),
        commitments: m.draft.commitments.map((c) => ({ ...c })),
        issues: m.draft.issues.map((i) => ({ ...i })),
      });
    } else setEdit(null);
  }

  useEffect(() => {
    load().catch((e) => setError(errMessage(e)));
  }, [id]);

  async function generate() {
    setBusy(true); setError(""); setMessage("");
    try {
      await api.post(`/api/meetings/${id}/generate-draft`);
      await load();
      setMessage("Draft generated in demo AI mode (suggestions only — not published until you approve).");
    } catch (e: unknown) { setError(errMessage(e)); }
    finally { setBusy(false); }
  }

  async function saveDraft() {
    if (!edit) return;
    setBusy(true); setError(""); setMessage("");
    try {
      await api.put(`/api/meetings/${id}/draft`, {
        summary: edit.summary,
        discussionPoints: edit.discussionPoints,
        decisions: edit.decisions.map((d) => ({
          id: d.id,
          statement: d.statement,
          context: d.context,
          sourceEvidence: d.sourceEvidence,
          reviewStatus: d.reviewStatus,
          needsReview: d.needsReview,
        })),
        commitments: edit.commitments.map((c) => ({
          id: c.id,
          action: c.action,
          suggestedOwner: c.suggestedOwner,
          suggestedDueDate: c.suggestedDueDate,
          sourceEvidence: c.sourceEvidence,
          reviewStatus: c.reviewStatus,
          needsReview: c.needsReview,
          needsReviewReason: c.needsReviewReason,
        })),
        issues: edit.issues.map((i) => ({
          id: i.id,
          title: i.title,
          description: i.description,
          sourceEvidence: i.sourceEvidence,
          reviewStatus: i.reviewStatus,
          needsReview: i.needsReview,
        })),
      });
      await load();
      setMessage("Draft saved (not published).");
    } catch (e: unknown) { setError(errMessage(e)); }
    finally { setBusy(false); }
  }

  async function publish(forceRetry = false) {
    setBusy(true); setError(""); setMessage("");
    try {
      const result = await api.post<{ succeeded: boolean; message: string; tasksFailed: number; errors: string[] }>(
        `/api/meetings/${id}/publish`,
        { forceRetryFailedTasks: forceRetry }
      );
      await load();
      setMessage(result.message + (result.errors?.length ? ` ${result.errors.join(" ")}` : ""));
      if (result.succeeded) router.push("/commitments");
    } catch (e: unknown) { setError(errMessage(e)); }
    finally { setBusy(false); }
  }

  function setDecision(i: number, patch: Partial<DraftItem>) {
    if (!edit) return;
    const decisions = [...edit.decisions];
    decisions[i] = { ...decisions[i], ...patch };
    setEdit({ ...edit, decisions });
  }

  function setCommitment(i: number, patch: Partial<DraftItem>) {
    if (!edit) return;
    const commitments = [...edit.commitments];
    commitments[i] = { ...commitments[i], ...patch };
    setEdit({ ...edit, commitments });
  }

  function setIssue(i: number, patch: Partial<DraftItem>) {
    if (!edit) return;
    const issues = [...edit.issues];
    issues[i] = { ...issues[i], ...patch };
    setEdit({ ...edit, issues });
  }

  function addCommitment() {
    if (!edit) return;
    setEdit({
      ...edit,
      commitments: [
        ...edit.commitments,
        {
          id: crypto.randomUUID(),
          action: "",
          suggestedOwner: "",
          suggestedDueDate: "",
          sourceEvidence: "Added manually by reviewer",
          reviewStatus: "Proposed",
          needsReview: true,
          needsReviewReason: "Manual entry",
        },
      ],
    });
  }

  if (!meeting) return <p className="muted">{error || "Loading…"}</p>;

  return (
    <div className="stack">
      <PageHeader
        title={meeting.title}
        lede={`${meeting.department} · ${new Date(meeting.meetingDate).toLocaleString()} · ${meeting.organizer}`}
        actions={
          <>
            <span className="badge sim">{meeting.source}</span>
            <span className="badge neutral">{meeting.workflowStatus.replace(/([a-z])([A-Z])/g, "$1 $2")}</span>
            <Link className="link-btn" href="/meetings">← Meetings</Link>
          </>
        }
      />
      {error && <div className="error">{error}</div>}
      {message && <div className="notice">{message}</div>}

      <div className="panel">
        <h2 className="panel-title">Source transcript</h2>
        <div className="transcript">{meeting.transcript}</div>
        <div className="form-actions">
          <button className="primary" disabled={busy || meeting.draft?.isPublished} onClick={generate}>
            Generate Draft (AI)
          </button>
          <span className="badge ai">AI suggestions are not official records</span>
        </div>
      </div>

      {edit && (
        <div className="panel stack">
          <div className="row" style={{ justifyContent: "space-between" }}>
            <h2 style={{ margin: 0, fontSize: "1rem" }}>
              Human review{" "}
              {meeting.draft?.providerName && (
                <span className="badge ai">
                  {meeting.draft.providerName === "MockAi" ? "Demo AI (simulated)" : `AI: ${meeting.draft.providerName}`}
                </span>
              )}
            </h2>
            {meeting.draft?.isPublished && <span className="badge approved">Published</span>}
          </div>

          <div className="form-row">
            <label>Meeting summary</label>
            <textarea rows={3} value={edit.summary} onChange={(e) => setEdit({ ...edit, summary: e.target.value })} />
          </div>

          <h3 style={{ marginBottom: 0, fontSize: "0.95rem" }}>Proposed decisions</h3>
          {edit.decisions.map((d, i) => (
            <div key={d.id} className={`draft-card ${d.reviewStatus.toLowerCase()}`}>
              <div className="row">
                <span className={`badge ${d.reviewStatus === "Approved" ? "approved" : d.reviewStatus === "Rejected" ? "rejected" : "needs"}`}>
                  {d.reviewStatus}
                </span>
                <span className="badge ai">AI / draft</span>
              </div>
              <div className="form-row"><label>Statement</label>
                <input value={d.statement || ""} onChange={(e) => setDecision(i, { statement: e.target.value })} />
              </div>
              <div className="form-row"><label>Context</label>
                <input value={d.context || ""} onChange={(e) => setDecision(i, { context: e.target.value })} />
              </div>
              <div className="form-actions">
                <button type="button" onClick={() => setDecision(i, { reviewStatus: "Approved", needsReview: false })}>Approve</button>
                <button type="button" onClick={() => setDecision(i, { reviewStatus: "Rejected" })}>Reject</button>
                <button type="button" onClick={() => setDecision(i, { reviewStatus: "NeedsReview", needsReview: true })}>Needs review</button>
              </div>
              <div className="evidence">Evidence: {d.sourceEvidence}</div>
            </div>
          ))}

          <h3 style={{ marginBottom: 0, fontSize: "0.95rem" }}>Proposed commitments</h3>
          {edit.commitments.map((c, i) => (
            <div key={c.id} className={`draft-card ${c.reviewStatus.toLowerCase()} ${c.needsReview ? "needs" : ""}`}>
              <div className="row">
                <span className={`badge ${c.reviewStatus === "Approved" ? "approved" : c.reviewStatus === "Rejected" ? "rejected" : "needs"}`}>
                  {c.reviewStatus}
                </span>
                {c.needsReview && <span className="badge needs">Needs Review</span>}
              </div>
              <div className="form-row"><label>Action</label>
                <input value={c.action || ""} onChange={(e) => setCommitment(i, { action: e.target.value })} />
              </div>
              <div className="row">
                <div className="form-row" style={{ flex: 1 }}>
                  <label>Owner</label>
                  <input value={c.suggestedOwner || ""} onChange={(e) => setCommitment(i, { suggestedOwner: e.target.value, needsReview: !e.target.value })} />
                </div>
                <div className="form-row" style={{ flex: 1 }}>
                  <label>Due date</label>
                  <input type="date" value={c.suggestedDueDate ? c.suggestedDueDate.substring(0, 10) : ""} onChange={(e) => setCommitment(i, { suggestedDueDate: e.target.value, needsReview: !e.target.value })} />
                </div>
              </div>
              {c.needsReviewReason && <p className="muted" style={{ margin: 0 }}>Reason: {c.needsReviewReason}</p>}
              <div className="form-actions">
                <button type="button" onClick={() => setCommitment(i, { reviewStatus: "Approved", needsReview: false })}>Approve</button>
                <button type="button" onClick={() => setCommitment(i, { reviewStatus: "Rejected" })}>Reject</button>
              </div>
              <div className="evidence">Evidence: {c.sourceEvidence}</div>
            </div>
          ))}
          <button type="button" onClick={addCommitment}>Add commitment manually</button>

          <h3 style={{ marginBottom: 0, fontSize: "0.95rem" }}>Open issues</h3>
          {edit.issues.length === 0 && <p className="muted">No open issues extracted.</p>}
          {edit.issues.map((issue, i) => (
            <div key={issue.id} className={`draft-card ${issue.reviewStatus.toLowerCase()}`}>
              <div className="form-row"><label>Title</label>
                <input value={issue.title || ""} onChange={(e) => setIssue(i, { title: e.target.value })} />
              </div>
              <div className="form-row"><label>Description</label>
                <textarea rows={2} value={issue.description || ""} onChange={(e) => setIssue(i, { description: e.target.value })} />
              </div>
              <div className="form-actions">
                <button type="button" onClick={() => setIssue(i, { reviewStatus: "Approved" })}>Approve to Issues register</button>
                <button type="button" onClick={() => setIssue(i, { reviewStatus: "Rejected" })}>Reject</button>
              </div>
              <div className="evidence">Evidence: {issue.sourceEvidence}</div>
            </div>
          ))}

          <div className="form-actions">
            <button className="primary" disabled={busy || meeting.draft?.isPublished} onClick={saveDraft}>Save draft</button>
            <button className="primary" disabled={busy} onClick={() => publish(false)}>Approve & publish</button>
            <button disabled={busy} onClick={() => publish(true)}>Retry failed tasks</button>
          </div>
        </div>
      )}
    </div>
  );
}
