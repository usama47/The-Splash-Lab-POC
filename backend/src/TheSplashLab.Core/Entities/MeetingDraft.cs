namespace TheSplashLab.Core.Entities;

public class MeetingDraft
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;
    public string Summary { get; set; } = string.Empty;
    public string DiscussionPointsJson { get; set; } = "[]";
    public string ProviderName { get; set; } = "MockAi";
    public string RawExtractionJson { get; set; } = "{}";
    public bool IsPublished { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? LastEditedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public ICollection<DraftDecision> Decisions { get; set; } = new List<DraftDecision>();
    public ICollection<DraftCommitment> Commitments { get; set; } = new List<DraftCommitment>();
    public ICollection<DraftIssue> Issues { get; set; } = new List<DraftIssue>();
}
