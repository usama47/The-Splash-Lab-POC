using TheSplashLab.Core.Enums;

namespace TheSplashLab.Core.Entities;

public class DecisionRecord
{
    public Guid Id { get; set; }
    public string Statement { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public Guid? MeetingId { get; set; }
    public Meeting? Meeting { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public Guid? ApproverUserId { get; set; }
    public DateTime DecisionDateUtc { get; set; }
    public bool IsApproved { get; set; }
    public string Department { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class CommitmentRecord
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public Guid? OwnerUserId { get; set; }
    public DateTime? DueDate { get; set; }
    public CommitmentStatus Status { get; set; } = CommitmentStatus.Open;
    public Guid? MeetingId { get; set; }
    public Meeting? Meeting { get; set; }
    public Guid? DecisionId { get; set; }
    public DecisionRecord? Decision { get; set; }
    public string Department { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public DestinationTask? DestinationTask { get; set; }
    public ICollection<CommitmentStatusHistory> StatusHistory { get; set; } = new List<CommitmentStatusHistory>();
}

public class CommitmentStatusHistory
{
    public Guid Id { get; set; }
    public Guid CommitmentId { get; set; }
    public CommitmentRecord Commitment { get; set; } = null!;
    public CommitmentStatus FromStatus { get; set; }
    public CommitmentStatus ToStatus { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DestinationTask
{
    public Guid Id { get; set; }
    public Guid CommitmentId { get; set; }
    public CommitmentRecord Commitment { get; set; } = null!;
    public string ExternalTaskId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public TaskDestinationStatus Status { get; set; } = TaskDestinationStatus.Pending;
    public string? FailureReason { get; set; }
    public string DestinationName { get; set; } = "LocalTaskRepository";
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

public class IssueRecord
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public IssueStatus Status { get; set; } = IssueStatus.Open;
    public IssuePriority Priority { get; set; } = IssuePriority.Medium;
    public string? Resolution { get; set; }
    public Guid? MeetingId { get; set; }
    public Guid? KnowledgeArticleId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
}

public class KnowledgeArticle
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;
    public Guid? SourceIssueId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; set; }
}

public class ProcessRegisterEntry
{
    public Guid Id { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public ProcessRegion Region { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public decimal EstimatedHoursPerWeek { get; set; }
    public string SystemsUsed { get; set; } = string.Empty;
    public int PainScore { get; set; }
    public ProcessClassification Classification { get; set; }
    public string OpportunityForImprovement { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = "Mapped";
}

public class AuditLogEntry
{
    public Guid Id { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string ActorName { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public Guid? MeetingId { get; set; }
    public string? BeforeValues { get; set; }
    public string? AfterValues { get; set; }
    public bool Succeeded { get; set; } = true;
    public string? FailureReason { get; set; }
}

public class IntegrationSetting
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public IntegrationStatus Status { get; set; }
    public string ModeLabel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SetupNotes { get; set; } = string.Empty;
    public bool IsSimulated { get; set; }
}
