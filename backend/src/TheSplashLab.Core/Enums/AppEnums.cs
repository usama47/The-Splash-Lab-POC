namespace TheSplashLab.Core.Enums;

public enum UserRole
{
    Employee = 0,
    Reviewer = 1,
    Administrator = 2
}

public enum MeetingWorkflowStatus
{
    New = 0,
    DraftGenerated = 1,
    InReview = 2,
    Approved = 3,
    Published = 4,
    PartiallyPublished = 5,
    Failed = 6
}

public enum ItemReviewStatus
{
    Proposed = 0,
    Approved = 1,
    Rejected = 2,
    NeedsReview = 3
}

public enum CommitmentStatus
{
    Open = 0,
    InProgress = 1,
    Completed = 2,
    Overdue = 3,
    Cancelled = 4
}

public enum IssueStatus
{
    Open = 0,
    InProgress = 1,
    Resolved = 2,
    Closed = 3
}

public enum IssuePriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum ApprovalStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3
}

public enum ProcessClassification
{
    Automate = 0,
    Accelerate = 1,
    Elevate = 2
}

public enum ProcessRegion
{
    UK = 0,
    US = 1,
    Global = 2
}

public enum IntegrationStatus
{
    Simulated = 0,
    Connected = 1,
    NotConnected = 2,
    Misconfigured = 3,
    Error = 4
}

public enum TaskDestinationStatus
{
    Pending = 0,
    Created = 1,
    Failed = 2,
    Skipped = 3
}
