using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TheSplashLab.Core.Models;

public class ExtractionResult
{
    public string Summary { get; set; } = string.Empty;
    public List<string> DiscussionPoints { get; set; } = new();
    public List<ExtractedDecision> Decisions { get; set; } = new();
    public List<ExtractedCommitment> Commitments { get; set; } = new();
    public List<ExtractedIssue> OpenIssues { get; set; } = new();
    public string ProviderName { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
}

public class ExtractedDecision
{
    public string Statement { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string SourceEvidence { get; set; } = string.Empty;
    public bool NeedsReview { get; set; }
}

public class ExtractedCommitment
{
    public string Action { get; set; } = string.Empty;
    public string? SuggestedOwner { get; set; }
    public DateTime? SuggestedDueDate { get; set; }
    public string SourceEvidence { get; set; } = string.Empty;
    public bool NeedsReview { get; set; }
    public string NeedsReviewReason { get; set; } = string.Empty;
}

public class ExtractedIssue
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SourceEvidence { get; set; } = string.Empty;
    public bool NeedsReview { get; set; }
}

public class ExtractionRequest
{
    [Required]
    public Guid MeetingId { get; set; }
}

public class SaveDraftRequest
{
    /// <summary>Draft version the reviewer last saw; a mismatch means another reviewer changed the draft.</summary>
    public string? ExpectedVersion { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<string> DiscussionPoints { get; set; } = new();
    public List<DraftDecisionEdit> Decisions { get; set; } = new();
    public List<DraftCommitmentEdit> Commitments { get; set; } = new();
    public List<DraftIssueEdit> Issues { get; set; } = new();
}

public class DraftDecisionEdit
{
    public Guid? Id { get; set; }
    public string Statement { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string SourceEvidence { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = "Proposed";
    public bool NeedsReview { get; set; }
}

public class DraftCommitmentEdit
{
    public Guid? Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? SuggestedOwner { get; set; }
    public DateTime? SuggestedDueDate { get; set; }
    public string SourceEvidence { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = "Proposed";
    public bool NeedsReview { get; set; }
    public string NeedsReviewReason { get; set; } = string.Empty;
}

public class DraftIssueEdit
{
    public Guid? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SourceEvidence { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = "Proposed";
    public bool NeedsReview { get; set; }
}

public class PublishRequest
{
    public bool ForceRetryFailedTasks { get; set; }

    /// <summary>Draft version the reviewer last saw. Checked before any edit or publish is applied.</summary>
    public string? ExpectedVersion { get; set; }

    /// <summary>On-screen draft edits applied atomically before publishing, so no separate save is needed.</summary>
    public SaveDraftRequest? Draft { get; set; }
}

public class PublishResult
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid MeetingId { get; set; }
    public string DraftVersion { get; set; } = string.Empty;
    public int DecisionsPublished { get; set; }
    public int CommitmentsPublished { get; set; }

    /// <summary>Tasks that exist after this run: <see cref="TasksNewlyCreated"/> + <see cref="TasksAlreadyExisting"/>.</summary>
    public int TasksCreated { get; set; }
    public int TasksNewlyCreated { get; set; }
    public int TasksAlreadyExisting { get; set; }
    public int TasksFailed { get; set; }
    public List<PublishedTaskDto> Tasks { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class PublishedTaskDto
{
    public Guid CommitmentId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public Guid? TaskId { get; set; }
    public string ExternalTaskId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsNew { get; set; }
    public string? FailureReason { get; set; }
}

public class DraftConflictException : Exception
{
    public DraftConflictException()
        : base("Another reviewer changed this draft after you opened it. Reload the latest draft before publishing.")
    {
    }
}

public class CreateMeetingRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public DateTime MeetingDate { get; set; } = DateTime.UtcNow.Date;

    [Required, MaxLength(100)]
    public string Department { get; set; } = "Operations";

    [Required, MaxLength(100)]
    public string Organizer { get; set; } = string.Empty;

    [Required, MinLength(20), MaxLength(200_000)]
    public string Transcript { get; set; } = string.Empty;

    public string Source { get; set; } = "ManualUpload";
}

public class UpdateCommitmentStatusRequest
{
    [Required]
    public string Status { get; set; } = "Open";
    public string Note { get; set; } = string.Empty;
}

public class PublishKnowledgeFromIssueRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Description { get; set; } = string.Empty;
}

public class ApproveKnowledgeRequest
{
    public bool Approve { get; set; } = true;
}

public class TaskCreateRequest
{
    public Guid CommitmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public bool SimulateFailure { get; set; }
}

public class TaskCreateResult
{
    public bool Succeeded { get; set; }
    public string ExternalTaskId { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
}

public class DashboardDto
{
    public int MeetingsProcessed { get; set; }
    public int DraftsAwaitingApproval { get; set; }
    public int ApprovedDecisions { get; set; }
    public int OpenCommitments { get; set; }
    public int OverdueCommitments { get; set; }
    public int ProcessesMapped { get; set; }
    public List<WorkflowStepCount> WorkflowProgress { get; set; } = new();
}

public class WorkflowStepCount
{
    public string Step { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ProcessSummaryDto
{
    public string Classification { get; set; } = string.Empty;
    public decimal TotalHoursPerWeek { get; set; }
    public int ProcessCount { get; set; }
}

public class IntegrationStatusDto
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ModeLabel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SetupNotes { get; set; } = string.Empty;
    public bool IsSimulated { get; set; }
}

/// <summary>JSON shape expected from Claude / mock AI.</summary>
public class AiExtractionPayload
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("discussionPoints")]
    public List<string> DiscussionPoints { get; set; } = new();

    [JsonPropertyName("decisions")]
    public List<AiDecisionPayload> Decisions { get; set; } = new();

    [JsonPropertyName("commitments")]
    public List<AiCommitmentPayload> Commitments { get; set; } = new();

    [JsonPropertyName("openIssues")]
    public List<AiIssuePayload> OpenIssues { get; set; } = new();
}

public class AiDecisionPayload
{
    [JsonPropertyName("statement")]
    public string Statement { get; set; } = string.Empty;

    [JsonPropertyName("context")]
    public string Context { get; set; } = string.Empty;

    [JsonPropertyName("sourceEvidence")]
    public string SourceEvidence { get; set; } = string.Empty;

    [JsonPropertyName("needsReview")]
    public bool NeedsReview { get; set; }
}

public class AiCommitmentPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("suggestedOwner")]
    public string? SuggestedOwner { get; set; }

    [JsonPropertyName("suggestedDueDate")]
    public string? SuggestedDueDate { get; set; }

    [JsonPropertyName("sourceEvidence")]
    public string SourceEvidence { get; set; } = string.Empty;

    [JsonPropertyName("needsReview")]
    public bool NeedsReview { get; set; }

    [JsonPropertyName("needsReviewReason")]
    public string NeedsReviewReason { get; set; } = string.Empty;
}

public class AiIssuePayload
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("sourceEvidence")]
    public string SourceEvidence { get; set; } = string.Empty;

    [JsonPropertyName("needsReview")]
    public bool NeedsReview { get; set; }
}
