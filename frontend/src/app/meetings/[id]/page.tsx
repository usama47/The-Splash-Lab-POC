"use client";

import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { ApiError, DraftItem, MeetingDetail, PublishResult, api, errMessage } from "@/lib/api";
import { PageHeader } from "@/components/PageHeader";
import Link from "next/link";
import { toast } from "@/components/Toaster";

function publishToast(res: PublishResult) {
  if (res.succeeded) toast("Published successfully.");
  else toast("Published with some errors.", "warning");
}

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
  const [meeting, setMeeting] = useState<MeetingDetail | null>(null);
  const [edit, setEdit] = useState<Editable | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<PublishResult | null>(null);
  const [conflict, setConflict] = useState("");

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

  function draftPayload(e: Editable) {
    return {
      summary: e.summary,
      discussionPoints: e.discussionPoints,
      decisions: e.decisions.map((d) => ({
        id: d.id,
        statement: d.statement,
        context: d.context,
        sourceEvidence: d.sourceEvidence,
        reviewStatus: d.reviewStatus,
        needsReview: d.needsReview,
      })),
      commitments: e.commitments.map((c) => ({
        id: c.id,
        action: c.action,
        suggestedOwner: c.suggestedOwner || null,
        suggestedDueDate: c.suggestedDueDate || null,
        sourceEvidence: c.sourceEvidence,
        reviewStatus: c.reviewStatus,
        needsReview: c.needsReview,
        needsReviewReason: c.needsReviewReason,
      })),
      issues: e.issues.map((i) => ({
        id: i.id,
        title: i.title,
        description: i.description,
        sourceEvidence: i.sourceEvidence,
        reviewStatus: i.reviewStatus,
        needsReview: i.needsReview,
      })),
    };
  }

  function showConflict(e: unknown): boolean {
    if (e instanceof ApiError && e.status === 409) {
      setConflict(e.message);
      return true;
    }
    return false;
  }

  async function saveDraft() {
    if (!edit || !meeting?.draft) return;
    setBusy(true); setError(""); setMessage(""); setConflict("");
    try {
      await api.put(`/api/meetings/${id}/draft`, {
        ...draftPayload(edit),
        expectedVersion: meeting.draft.version,
      });
      await load();
      setMessage("Draft saved (not published).");
    } catch (e: unknown) { if (!showConflict(e)) setError(errMessage(e)); }
    finally { setBusy(false); }
  }

  // Sends the on-screen edits with the publish request, so no separate save is needed.
  async function publish() {
    if (!edit || !meeting?.draft) return;
    setBusy(true); setError(""); setMessage(""); setConflict(""); setResult(null);
    try {
      const res = await api.post<PublishResult>(`/api/meetings/${id}/publish`, {
        forceRetryFailedTasks: false,
        expectedVersion: meeting.draft.version,
        draft: draftPayload(edit),
      });
      setResult(res);
      publishToast(res);
      await load();
    } catch (e: unknown) { if (!showConflict(e)) setError(errMessage(e)); }
    finally { setBusy(false); }
  }

  async function retryFailed() {
    setBusy(true); setError(""); setMessage(""); setConflict("");
    try {
      const res = await api.post<PublishResult>(`/api/meetings/${id}/publish`, { forceRetryFailedTasks: true });
      setResult(res);
      publishToast(res);
      await load();
    } catch (e: unknown) { setError(errMessage(e)); }
    finally { setBusy(false); }
  }

  async function reloadLatest() {
    setConflict(""); setError(""); setResult(null);
    try {
      await load();
      setMessage("Loaded the latest draft from the server. Your unsaved on-screen changes were replaced.");
    } catch (e: unknown) { setError(errMessage(e)); }
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

      {conflict && (
        <div className="error stack">
          <strong>Conflict — nothing was published.</strong>
          <span>{conflict}</span>
          <div className="form-actions">
            <button className="primary" onClick={reloadLatest}>Reload latest draft</button>
          </div>
        </div>
      )}

      {result && (
        <div className="panel stack">
          <div className="row" style={{ justifyContent: "space-between" }}>
            <h2 className="panel-title" style={{ margin: 0 }}>Publish results</h2>
            <span className={`badge ${result.succeeded ? "approved" : "rejected"}`}>
              {result.succeeded ? "All tasks created" : "Retry needed"}
            </span>
          </div>
          <p style={{ margin: 0 }}>{result.message}</p>
          <div className="grid-cards">
            <div className="stat"><div className="label">Newly created tasks</div><div className="value">{result.tasksNewlyCreated}</div></div>
            <div className="stat"><div className="label">Already existed</div><div className="value">{result.tasksAlreadyExisting}</div></div>
            <div className="stat"><div className="label">Failed</div><div className="value">{result.tasksFailed}</div></div>
          </div>
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr><th>Action</th><th>Owner</th><th>Due</th><th>Task</th><th>Outcome</th><th>Records</th></tr>
              </thead>
              <tbody>
                {result.tasks.map((t) => (
                  <tr key={t.commitmentId}>
                    <td>{t.action}</td>
                    <td>{t.owner}</td>
                    <td>{t.dueDate ? t.dueDate.substring(0, 10) : "—"}</td>
                    <td>
                      {t.externalTaskId || "—"}
                      {t.taskId && <div className="muted" style={{ fontSize: "0.75rem" }}>ID {t.taskId.slice(0, 8)}</div>}
                    </td>
                    <td>
                      <span className={`badge ${t.status === "Created" ? "approved" : "rejected"}`}>
                        {t.status === "Created" ? (t.isNew ? "Created now" : "Already existed") : "Failed"}
                      </span>
                      {t.failureReason && <div className="muted" style={{ fontSize: "0.78rem" }}>{t.failureReason}</div>}
                    </td>
                    <td><Link href={`/commitments#${t.commitmentId}`}>Commitment</Link></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="form-actions">
            {result.tasksFailed > 0 && (
              <button className="primary" disabled={busy} onClick={retryFailed}>
                Retry failed task{result.tasksFailed === 1 ? "" : "s"}
              </button>
            )}
            <Link className="link-btn" href="/commitments">All commitments →</Link>
            <Link className="link-btn" href={`/audit?meetingId=${result.meetingId}`}>Audit records for this meeting →</Link>
          </div>
        </div>
      )}

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
                <button type="button" onClick={() => { setDecision(i, { reviewStatus: "Approved", needsReview: false }); toast("Approved successfully."); }}>Approve</button>
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
                <button type="button" onClick={() => { setCommitment(i, { reviewStatus: "Approved", needsReview: false }); toast("Approved successfully."); }}>Approve</button>
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
                <button type="button" onClick={() => { setIssue(i, { reviewStatus: "Approved" }); toast("Approved successfully."); }}>Approve to Issues register</button>
                <button type="button" onClick={() => setIssue(i, { reviewStatus: "Rejected" })}>Reject</button>
              </div>
              <div className="evidence">Evidence: {issue.sourceEvidence}</div>
            </div>
          ))}

          <div className="form-actions">
            <button className="primary" disabled={busy || meeting.draft?.isPublished} onClick={saveDraft}>Save draft</button>
            <button className="primary" disabled={busy || meeting.draft?.isPublished} onClick={publish}>Approve & publish</button>
            {meeting.workflowStatus === "PartiallyPublished" && !result && (
              <button disabled={busy} onClick={retryFailed}>Retry failed tasks</button>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
