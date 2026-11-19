using TheSplashLab.Core.Enums;

namespace TheSplashLab.Core.Entities;

public class Meeting
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime MeetingDate { get; set; }
    public string Department { get; set; } = string.Empty;
    public string Organizer { get; set; } = string.Empty;
    public string Source { get; set; } = "LocalSeed";
    public string Transcript { get; set; } = string.Empty;
    public string ScenarioKey { get; set; } = string.Empty;
    public MeetingWorkflowStatus WorkflowStatus { get; set; } = MeetingWorkflowStatus.New;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public MeetingDraft? Draft { get; set; }
    public ICollection<DecisionRecord> Decisions { get; set; } = new List<DecisionRecord>();
    public ICollection<CommitmentRecord> Commitments { get; set; } = new List<CommitmentRecord>();
}
