using TheSplashLab.Core.Entities;
using TheSplashLab.Core.Models;

namespace TheSplashLab.Core.Interfaces;

public interface IMeetingSource
{
    Task<IReadOnlyList<Meeting>> GetMeetingsAsync(string? department = null, CancellationToken ct = default);
    Task<Meeting?> GetMeetingAsync(Guid id, CancellationToken ct = default);
    Task<Meeting> CreateMeetingAsync(CreateMeetingRequest request, CancellationToken ct = default);
}

public interface IAiExtractionService
{
    string ProviderName { get; }
    bool IsExternal { get; }
    Task<ExtractionResult> ExtractAsync(Meeting meeting, CancellationToken ct = default);
}

public interface IKnowledgeRepository
{
    Task<IReadOnlyList<KnowledgeArticle>> ListAsync(string? search = null, bool approvedOnly = false, CancellationToken ct = default);
    Task<KnowledgeArticle?> GetAsync(Guid id, CancellationToken ct = default);
    Task<KnowledgeArticle> CreateDraftFromIssueAsync(Guid issueId, string title, string description, string ownerName, CancellationToken ct = default);
    Task<KnowledgeArticle> SetApprovalAsync(Guid id, bool approve, Guid actorUserId, string actorName, CancellationToken ct = default);
}

public interface IRegisterRepository
{
    Task<IReadOnlyList<DecisionRecord>> ListDecisionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CommitmentRecord>> ListCommitmentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<IssueRecord>> ListIssuesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProcessRegisterEntry>> ListProcessesAsync(CancellationToken ct = default);
    Task<DecisionRecord?> FindDecisionByIdempotencyAsync(string key, CancellationToken ct = default);
    Task<CommitmentRecord?> FindCommitmentByIdempotencyAsync(string key, CancellationToken ct = default);
    Task AddDecisionAsync(DecisionRecord decision, CancellationToken ct = default);
    Task AddCommitmentAsync(CommitmentRecord commitment, CancellationToken ct = default);
    Task AddIssueAsync(IssueRecord issue, CancellationToken ct = default);
    Task UpdateCommitmentAsync(CommitmentRecord commitment, CancellationToken ct = default);
    Task UpdateIssueAsync(IssueRecord issue, CancellationToken ct = default);
}

public interface ITaskDestination
{
    string DestinationName { get; }
    bool IsSimulated { get; }
    Task<TaskCreateResult> CreateTaskAsync(TaskCreateRequest request, CancellationToken ct = default);
}

public interface IAuditLogService
{
    Task RecordAsync(
        string actorName,
        Guid? actorUserId,
        string action,
        string entityType,
        string? entityId,
        Guid? meetingId,
        string? beforeValues,
        string? afterValues,
        bool succeeded = true,
        string? failureReason = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<AuditLogEntry>> ListAsync(int take = 200, CancellationToken ct = default);
}

public interface ICurrentUserAccessor
{
    Guid UserId { get; }
    string DisplayName { get; }
    string Email { get; }
    string Role { get; }
    bool IsReviewer { get; }
    bool IsAdministrator { get; }
}
