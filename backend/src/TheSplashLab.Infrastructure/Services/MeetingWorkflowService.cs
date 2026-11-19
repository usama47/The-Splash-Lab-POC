using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TheSplashLab.Core.Entities;
using TheSplashLab.Core.Enums;
using TheSplashLab.Core.Interfaces;
using TheSplashLab.Core.Models;
using TheSplashLab.Infrastructure.Data;
using TheSplashLab.Infrastructure.Repositories;

namespace TheSplashLab.Infrastructure.Services;

public class MeetingWorkflowService
{
    private readonly AppDbContext _db;
    private readonly IAiExtractionService _ai;
    private readonly ITaskDestination _tasks;
    private readonly IAuditLogService _audit;
    private readonly IRegisterRepository _registers;
    private readonly ICurrentUserAccessor _user;

    public MeetingWorkflowService(
        AppDbContext db,
        IAiExtractionService ai,
        ITaskDestination tasks,
        IAuditLogService audit,
        IRegisterRepository registers,
        ICurrentUserAccessor user)
    {
        _db = db;
        _ai = ai;
        _tasks = tasks;
        _audit = audit;
        _registers = registers;
        _user = user;
    }

    public async Task<MeetingDraft> GenerateDraftAsync(Guid meetingId, CancellationToken ct = default)
    {
        var meeting = await _db.Meetings
            .Include(m => m.Draft)!.ThenInclude(d => d!.Decisions)
            .Include(m => m.Draft)!.ThenInclude(d => d!.Commitments)
            .Include(m => m.Draft)!.ThenInclude(d => d!.Issues)
            .FirstOrDefaultAsync(m => m.Id == meetingId, ct)
            ?? throw new InvalidOperationException("Meeting not found.");

        if (string.IsNullOrWhiteSpace(meeting.Transcript))
            throw new InvalidOperationException("Cannot extract from empty transcript.");

        if (meeting.Draft?.IsPublished == true)
            throw new InvalidOperationException("Meeting already published. Create a new meeting for further extraction.");

        var extraction = await _ai.ExtractAsync(meeting, ct);

        MeetingDraft draft;
        if (meeting.Draft is null)
        {
            draft = new MeetingDraft
            {
                Id = Guid.NewGuid(),
                MeetingId = meeting.Id
            };
            _db.MeetingDrafts.Add(draft);
            meeting.Draft = draft;
        }
        else
        {
            draft = meeting.Draft;
            _db.DraftDecisions.RemoveRange(draft.Decisions);
            _db.DraftCommitments.RemoveRange(draft.Commitments);
            _db.DraftIssues.RemoveRange(draft.Issues);
            draft.Decisions.Clear();
            draft.Commitments.Clear();
            draft.Issues.Clear();
        }

        draft.Summary = extraction.Summary;
        draft.DiscussionPointsJson = JsonSerializer.Serialize(extraction.DiscussionPoints);
        draft.ProviderName = extraction.ProviderName;
        draft.RawExtractionJson = extraction.RawJson;
        draft.IsPublished = false;
        draft.UpdatedAtUtc = DateTime.UtcNow;
        draft.LastEditedByUserId = _user.UserId;

        foreach (var d in extraction.Decisions)
        {
            draft.Decisions.Add(new DraftDecision
            {
                Id = Guid.NewGuid(),
                MeetingDraftId = draft.Id,
                Statement = d.Statement,
                Context = d.Context,
                SourceEvidence = d.SourceEvidence,
                NeedsReview = d.NeedsReview,
                ReviewStatus = d.NeedsReview ? ItemReviewStatus.NeedsReview : ItemReviewStatus.Proposed
            });
        }

        foreach (var c in extraction.Commitments)
        {
            draft.Commitments.Add(new DraftCommitment
            {
                Id = Guid.NewGuid(),
                MeetingDraftId = draft.Id,
                Action = c.Action,
                SuggestedOwner = c.SuggestedOwner,
                SuggestedDueDate = c.SuggestedDueDate,
                SourceEvidence = c.SourceEvidence,
                NeedsReview = c.NeedsReview,
                NeedsReviewReason = c.NeedsReviewReason,
                ReviewStatus = c.NeedsReview ? ItemReviewStatus.NeedsReview : ItemReviewStatus.Proposed
            });
        }

        foreach (var i in extraction.OpenIssues)
        {
            draft.Issues.Add(new DraftIssue
            {
                Id = Guid.NewGuid(),
                MeetingDraftId = draft.Id,
                Title = i.Title,
                Description = i.Description,
                SourceEvidence = i.SourceEvidence,
                NeedsReview = i.NeedsReview,
                ReviewStatus = i.NeedsReview ? ItemReviewStatus.NeedsReview : ItemReviewStatus.Proposed
            });
        }

        meeting.WorkflowStatus = MeetingWorkflowStatus.DraftGenerated;
        meeting.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(
            _user.DisplayName, _user.UserId, "GenerateDraft", "MeetingDraft", draft.Id.ToString(),
            meeting.Id, null, $"Provider={extraction.ProviderName}; decisions={draft.Decisions.Count}; commitments={draft.Commitments.Count}",
            true, null, ct);

        return await LoadDraftAsync(draft.Id, ct);
    }

    public async Task<MeetingDraft> SaveDraftAsync(Guid meetingId, SaveDraftRequest request, CancellationToken ct = default)
    {
        RequireReviewer();

        var meeting = await _db.Meetings
            .Include(m => m.Draft)!.ThenInclude(d => d!.Decisions)
            .Include(m => m.Draft)!.ThenInclude(d => d!.Commitments)
            .Include(m => m.Draft)!.ThenInclude(d => d!.Issues)
            .FirstOrDefaultAsync(m => m.Id == meetingId, ct)
            ?? throw new InvalidOperationException("Meeting not found.");

        if (meeting.Draft is null)
            throw new InvalidOperationException("No draft exists. Generate a draft first.");
        if (meeting.Draft.IsPublished)
            throw new InvalidOperationException("Published drafts cannot be edited.");

        var draft = meeting.Draft;
        var before = JsonSerializer.Serialize(new { draft.Summary, Decisions = draft.Decisions.Count, Commitments = draft.Commitments.Count });

        draft.Summary = request.Summary.Trim();
        draft.DiscussionPointsJson = JsonSerializer.Serialize(request.DiscussionPoints ?? new());
        draft.UpdatedAtUtc = DateTime.UtcNow;
        draft.LastEditedByUserId = _user.UserId;

        _db.DraftDecisions.RemoveRange(draft.Decisions);
        _db.DraftCommitments.RemoveRange(draft.Commitments);
        _db.DraftIssues.RemoveRange(draft.Issues);
        draft.Decisions.Clear();
        draft.Commitments.Clear();
        draft.Issues.Clear();

        foreach (var d in request.Decisions ?? new())
        {
            draft.Decisions.Add(new DraftDecision
            {
                Id = d.Id ?? Guid.NewGuid(),
                MeetingDraftId = draft.Id,
                Statement = d.Statement.Trim(),
                Context = d.Context?.Trim() ?? string.Empty,
                SourceEvidence = d.SourceEvidence?.Trim() ?? string.Empty,
                NeedsReview = d.NeedsReview,
                ReviewStatus = ParseReviewStatus(d.ReviewStatus, d.NeedsReview)
            });
        }

        foreach (var c in request.Commitments ?? new())
        {
            var status = ParseReviewStatus(c.ReviewStatus, c.NeedsReview);
            var needsReview = c.NeedsReview ||
                              string.IsNullOrWhiteSpace(c.SuggestedOwner) ||
                              !c.SuggestedDueDate.HasValue;

            if (status == ItemReviewStatus.Approved && needsReview &&
                (string.IsNullOrWhiteSpace(c.SuggestedOwner) || !c.SuggestedDueDate.HasValue))
            {
                // Keep as needs review if still incomplete; reviewer must fill fields
                status = ItemReviewStatus.NeedsReview;
            }

            draft.Commitments.Add(new DraftCommitment
            {
                Id = c.Id ?? Guid.NewGuid(),
                MeetingDraftId = draft.Id,
                Action = c.Action.Trim(),
                SuggestedOwner = string.IsNullOrWhiteSpace(c.SuggestedOwner) ? null : c.SuggestedOwner.Trim(),
                SuggestedDueDate = c.SuggestedDueDate?.Date,
                SourceEvidence = c.SourceEvidence?.Trim() ?? string.Empty,
                NeedsReview = needsReview && status != ItemReviewStatus.Rejected,
                NeedsReviewReason = c.NeedsReviewReason?.Trim() ?? string.Empty,
                ReviewStatus = status
            });
        }

        foreach (var i in request.Issues ?? new())
        {
            draft.Issues.Add(new DraftIssue
            {
                Id = i.Id ?? Guid.NewGuid(),
                MeetingDraftId = draft.Id,
                Title = i.Title.Trim(),
                Description = i.Description?.Trim() ?? string.Empty,
                SourceEvidence = i.SourceEvidence?.Trim() ?? string.Empty,
                NeedsReview = i.NeedsReview,
                ReviewStatus = ParseReviewStatus(i.ReviewStatus, i.NeedsReview)
            });
        }

        meeting.WorkflowStatus = MeetingWorkflowStatus.InReview;
        meeting.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(
            _user.DisplayName, _user.UserId, "SaveDraft", "MeetingDraft", draft.Id.ToString(),
            meeting.Id, before, JsonSerializer.Serialize(new { draft.Summary, Decisions = draft.Decisions.Count, Commitments = draft.Commitments.Count }),
            true, null, ct);

        return await LoadDraftAsync(draft.Id, ct);
    }

    public async Task<PublishResult> PublishAsync(Guid meetingId, PublishRequest request, CancellationToken ct = default)
    {
        RequireReviewer();

        var meeting = await _db.Meetings
            .Include(m => m.Draft)!.ThenInclude(d => d!.Decisions)
            .Include(m => m.Draft)!.ThenInclude(d => d!.Commitments)
            .Include(m => m.Draft)!.ThenInclude(d => d!.Issues)
            .FirstOrDefaultAsync(m => m.Id == meetingId, ct)
            ?? throw new InvalidOperationException("Meeting not found.");

        if (meeting.Draft is null)
            throw new InvalidOperationException("No draft to publish.");

        var draft = meeting.Draft;
        var result = new PublishResult();

        var approvedDecisions = draft.Decisions.Where(d => d.ReviewStatus == ItemReviewStatus.Approved).ToList();
        var approvedCommitments = draft.Commitments.Where(c => c.ReviewStatus == ItemReviewStatus.Approved).ToList();
        var rejectedCommitments = draft.Commitments.Where(c => c.ReviewStatus == ItemReviewStatus.Rejected).ToList();

        if (approvedDecisions.Count == 0 && approvedCommitments.Count == 0)
        {
            result.Succeeded = false;
            result.Message = "No approved decisions or commitments to publish.";
            result.Errors.Add(result.Message);
            return result;
        }

        foreach (var c in approvedCommitments)
        {
            if (string.IsNullOrWhiteSpace(c.SuggestedOwner) || !c.SuggestedDueDate.HasValue)
            {
                result.Succeeded = false;
                result.Message = "Approved commitments must have owner and due date.";
                result.Errors.Add($"Commitment '{c.Action}' is missing owner or due date.");
            }
        }

        if (result.Errors.Count > 0)
            return result;

        Guid? firstDecisionId = null;

        foreach (var d in approvedDecisions)
        {
            var key = $"decision:{meeting.Id}:{StableHash(d.Statement)}";
            var existing = await _registers.FindDecisionByIdempotencyAsync(key, ct);
            if (existing is not null)
            {
                d.PublishedDecisionId = existing.Id;
                firstDecisionId ??= existing.Id;
                result.DecisionsPublished++;
                continue;
            }

            var record = new DecisionRecord
            {
                Id = Guid.NewGuid(),
                Statement = d.Statement,
                Context = d.Context,
                MeetingId = meeting.Id,
                ApproverName = _user.DisplayName,
                ApproverUserId = _user.UserId,
                DecisionDateUtc = DateTime.UtcNow,
                IsApproved = true,
                Department = meeting.Department,
                IdempotencyKey = key
            };
            await _registers.AddDecisionAsync(record, ct);
            d.PublishedDecisionId = record.Id;
            firstDecisionId ??= record.Id;
            result.DecisionsPublished++;
        }

        foreach (var c in approvedCommitments)
        {
            var key = $"commitment:{meeting.Id}:{StableHash(c.Action)}";
            var existing = await _registers.FindCommitmentByIdempotencyAsync(key, ct);
            CommitmentRecord commitment;

            if (existing is not null)
            {
                commitment = existing;
                c.PublishedCommitmentId = existing.Id;
                result.CommitmentsPublished++;
            }
            else
            {
                commitment = new CommitmentRecord
                {
                    Id = Guid.NewGuid(),
                    Action = c.Action,
                    OwnerName = c.SuggestedOwner!,
                    DueDate = c.SuggestedDueDate,
                    Status = CommitmentStatusHelper.ComputeStatus(new CommitmentRecord
                    {
                        DueDate = c.SuggestedDueDate,
                        Status = CommitmentStatus.Open
                    }),
                    MeetingId = meeting.Id,
                    DecisionId = firstDecisionId,
                    Department = meeting.Department,
                    IdempotencyKey = key,
                    CreatedAtUtc = DateTime.UtcNow
                };
                commitment.StatusHistory.Add(new CommitmentStatusHistory
                {
                    Id = Guid.NewGuid(),
                    CommitmentId = commitment.Id,
                    FromStatus = CommitmentStatus.Open,
                    ToStatus = commitment.Status,
                    ChangedBy = _user.DisplayName,
                    Note = "Published from meeting draft",
                    ChangedAtUtc = DateTime.UtcNow
                });
                await _registers.AddCommitmentAsync(commitment, ct);
                c.PublishedCommitmentId = commitment.Id;
                result.CommitmentsPublished++;
            }

            // Task creation (idempotent)
            var taskKey = $"task:{commitment.Id}";
            var existingTask = await _db.DestinationTasks.FirstOrDefaultAsync(t => t.IdempotencyKey == taskKey, ct);

            if (existingTask is not null && existingTask.Status == TaskDestinationStatus.Created)
            {
                result.TasksCreated++;
                continue;
            }

            if (existingTask is not null &&
                existingTask.Status == TaskDestinationStatus.Failed &&
                !request.ForceRetryFailedTasks)
            {
                result.TasksFailed++;
                result.Errors.Add($"Task previously failed for '{commitment.Action}': {existingTask.FailureReason}");
                continue;
            }

            var createReq = new TaskCreateRequest
            {
                CommitmentId = commitment.Id,
                Title = commitment.Action,
                Assignee = commitment.OwnerName,
                DueDate = commitment.DueDate,
                IdempotencyKey = taskKey,
                SimulateFailure = meeting.ScenarioKey == "fail-destination" &&
                                  commitment.Action.Contains("FAIL_DESTINATION", StringComparison.OrdinalIgnoreCase)
            };

            // Only FAIL_DESTINATION commitments should fail in scenario 3; others succeed
            if (meeting.ScenarioKey == "fail-destination" &&
                !commitment.Action.Contains("FAIL_DESTINATION", StringComparison.OrdinalIgnoreCase))
            {
                createReq.SimulateFailure = false;
            }

            var taskResult = await _tasks.CreateTaskAsync(createReq, ct);

            if (existingTask is null)
            {
                existingTask = new DestinationTask
                {
                    Id = Guid.NewGuid(),
                    CommitmentId = commitment.Id,
                    IdempotencyKey = taskKey,
                    DestinationName = _tasks.DestinationName,
                    Title = commitment.Action,
                    Assignee = commitment.OwnerName,
                    DueDate = commitment.DueDate,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _db.DestinationTasks.Add(existingTask);
            }

            if (taskResult.Succeeded)
            {
                existingTask.Status = TaskDestinationStatus.Created;
                existingTask.ExternalTaskId = taskResult.ExternalTaskId;
                existingTask.FailureReason = null;
                existingTask.UpdatedAtUtc = DateTime.UtcNow;
                result.TasksCreated++;
            }
            else
            {
                existingTask.Status = TaskDestinationStatus.Failed;
                existingTask.FailureReason = taskResult.FailureReason;
                existingTask.UpdatedAtUtc = DateTime.UtcNow;
                result.TasksFailed++;
                result.Errors.Add(taskResult.FailureReason ?? "Task destination failed.");
            }
        }

        // Rejected items are explicitly not published
        foreach (var rejected in rejectedCommitments)
        {
            await _audit.RecordAsync(
                _user.DisplayName, _user.UserId, "RejectCommitment", "DraftCommitment", rejected.Id.ToString(),
                meeting.Id, null, rejected.Action, true, null, ct);
        }

        // Publish approved draft issues into issues register (idempotent-ish by title+meeting)
        foreach (var issue in draft.Issues.Where(i => i.ReviewStatus == ItemReviewStatus.Approved))
        {
            if (issue.PublishedIssueId.HasValue) continue;
            var record = new IssueRecord
            {
                Id = Guid.NewGuid(),
                Title = issue.Title,
                Description = issue.Description,
                Department = meeting.Department,
                OwnerName = "Unassigned",
                Status = IssueStatus.Open,
                Priority = IssuePriority.Medium,
                MeetingId = meeting.Id
            };
            await _registers.AddIssueAsync(record, ct);
            issue.PublishedIssueId = record.Id;
        }

        draft.IsPublished = result.TasksFailed == 0;
        draft.ApprovedByUserId = _user.UserId;
        draft.ApprovedAtUtc = DateTime.UtcNow;
        draft.UpdatedAtUtc = DateTime.UtcNow;

        meeting.WorkflowStatus = result.TasksFailed > 0
            ? MeetingWorkflowStatus.PartiallyPublished
            : MeetingWorkflowStatus.Published;
        meeting.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        result.Succeeded = result.TasksFailed == 0;
        result.Message = result.Succeeded
            ? "Published successfully."
            : "Published with task destination failures. Retry is safe (idempotent).";

        await _audit.RecordAsync(
            _user.DisplayName, _user.UserId, "PublishDraft", "Meeting", meeting.Id.ToString(),
            meeting.Id, null,
            JsonSerializer.Serialize(result),
            result.Succeeded,
            result.Succeeded ? null : string.Join("; ", result.Errors),
            ct);

        return result;
    }

    public async Task<CommitmentRecord> UpdateCommitmentStatusAsync(Guid commitmentId, UpdateCommitmentStatusRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<CommitmentStatus>(request.Status, true, out var status))
            throw new ArgumentException("Invalid commitment status.");

        var commitment = await _db.Commitments
            .Include(c => c.StatusHistory)
            .FirstOrDefaultAsync(c => c.Id == commitmentId, ct)
            ?? throw new InvalidOperationException("Commitment not found.");

        var from = commitment.Status;
        commitment.Status = status;
        if (status is not CommitmentStatus.Completed and not CommitmentStatus.Cancelled)
            commitment.Status = CommitmentStatusHelper.ComputeStatus(commitment);

        commitment.UpdatedAtUtc = DateTime.UtcNow;
        commitment.StatusHistory.Add(new CommitmentStatusHistory
        {
            Id = Guid.NewGuid(),
            CommitmentId = commitment.Id,
            FromStatus = from,
            ToStatus = commitment.Status,
            ChangedBy = _user.DisplayName,
            Note = request.Note ?? string.Empty,
            ChangedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(
            _user.DisplayName, _user.UserId, "UpdateCommitmentStatus", "Commitment", commitment.Id.ToString(),
            commitment.MeetingId, from.ToString(), commitment.Status.ToString(), true, null, ct);

        return commitment;
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var meetings = await _db.Meetings.AsNoTracking().ToListAsync(ct);
        var decisions = await _db.Decisions.AsNoTracking().CountAsync(d => d.IsApproved, ct);
        var commitments = await _db.Commitments.AsNoTracking().ToListAsync(ct);
        foreach (var c in commitments)
            CommitmentStatusHelper.RefreshOverdue(c);

        var processes = await _db.ProcessRegister.AsNoTracking().CountAsync(ct);

        return new DashboardDto
        {
            MeetingsProcessed = meetings.Count(m => m.WorkflowStatus is MeetingWorkflowStatus.Published or MeetingWorkflowStatus.PartiallyPublished or MeetingWorkflowStatus.Approved),
            DraftsAwaitingApproval = meetings.Count(m => m.WorkflowStatus is MeetingWorkflowStatus.DraftGenerated or MeetingWorkflowStatus.InReview),
            ApprovedDecisions = decisions,
            OpenCommitments = commitments.Count(c => c.Status is CommitmentStatus.Open or CommitmentStatus.InProgress),
            OverdueCommitments = commitments.Count(c => CommitmentStatusHelper.ComputeStatus(c) == CommitmentStatus.Overdue),
            ProcessesMapped = processes,
            WorkflowProgress = new List<WorkflowStepCount>
            {
                new() { Step = "New", Count = meetings.Count(m => m.WorkflowStatus == MeetingWorkflowStatus.New) },
                new() { Step = "Draft", Count = meetings.Count(m => m.WorkflowStatus == MeetingWorkflowStatus.DraftGenerated) },
                new() { Step = "In Review", Count = meetings.Count(m => m.WorkflowStatus == MeetingWorkflowStatus.InReview) },
                new() { Step = "Published", Count = meetings.Count(m => m.WorkflowStatus == MeetingWorkflowStatus.Published) },
                new() { Step = "Partial / Failed", Count = meetings.Count(m => m.WorkflowStatus is MeetingWorkflowStatus.PartiallyPublished or MeetingWorkflowStatus.Failed) }
            }
        };
    }

    private async Task<MeetingDraft> LoadDraftAsync(Guid draftId, CancellationToken ct)
        => await _db.MeetingDrafts
            .Include(d => d.Decisions)
            .Include(d => d.Commitments)
            .Include(d => d.Issues)
            .Include(d => d.Meeting)
            .FirstAsync(d => d.Id == draftId, ct);

    private void RequireReviewer()
    {
        if (!_user.IsReviewer && !_user.IsAdministrator)
            throw new UnauthorizedAccessException("Only Reviewer or Administrator roles may edit/approve drafts.");
    }

    private static ItemReviewStatus ParseReviewStatus(string? value, bool needsReview)
    {
        if (Enum.TryParse<ItemReviewStatus>(value, true, out var status))
            return status;
        return needsReview ? ItemReviewStatus.NeedsReview : ItemReviewStatus.Proposed;
    }

    private static string StableHash(string input)
    {
        var normalized = (input ?? string.Empty).Trim().ToLowerInvariant();
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes)[..16];
    }
}
