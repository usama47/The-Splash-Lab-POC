using TheSplashLab.Core.Enums;

namespace TheSplashLab.Core.Entities;

public class DraftDecision
{
    public Guid Id { get; set; }
    public Guid MeetingDraftId { get; set; }
    public MeetingDraft MeetingDraft { get; set; } = null!;
    public string Statement { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string SourceEvidence { get; set; } = string.Empty;
    public ItemReviewStatus ReviewStatus { get; set; } = ItemReviewStatus.Proposed;
    public bool NeedsReview { get; set; }
    public Guid? PublishedDecisionId { get; set; }
}

public class DraftCommitment
{
    public Guid Id { get; set; }
    public Guid MeetingDraftId { get; set; }
    public MeetingDraft MeetingDraft { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public string? SuggestedOwner { get; set; }
    public DateTime? SuggestedDueDate { get; set; }
    public string SourceEvidence { get; set; } = string.Empty;
    public ItemReviewStatus ReviewStatus { get; set; } = ItemReviewStatus.Proposed;
    public bool NeedsReview { get; set; }
    public string NeedsReviewReason { get; set; } = string.Empty;
    public Guid? PublishedCommitmentId { get; set; }
    public Guid? LinkedDraftDecisionId { get; set; }
}

public class DraftIssue
{
    public Guid Id { get; set; }
    public Guid MeetingDraftId { get; set; }
    public MeetingDraft MeetingDraft { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SourceEvidence { get; set; } = string.Empty;
    public ItemReviewStatus ReviewStatus { get; set; } = ItemReviewStatus.Proposed;
    public bool NeedsReview { get; set; }
    public Guid? PublishedIssueId { get; set; }
}
