using Microsoft.EntityFrameworkCore;
using TheSplashLab.Core.Entities;
using TheSplashLab.Core.Enums;
using TheSplashLab.Core.Interfaces;
using TheSplashLab.Core.Models;
using TheSplashLab.Infrastructure.Data;

namespace TheSplashLab.Infrastructure.Repositories;

public class MeetingSource : IMeetingSource
{
    private readonly AppDbContext _db;

    public MeetingSource(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Meeting>> GetMeetingsAsync(string? department = null, CancellationToken ct = default)
    {
        var q = _db.Meetings.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(department))
            q = q.Where(m => m.Department == department);
        return await q.OrderByDescending(m => m.MeetingDate).ToListAsync(ct);
    }

    public Task<Meeting?> GetMeetingAsync(Guid id, CancellationToken ct = default)
        => _db.Meetings
            .Include(m => m.Draft)!.ThenInclude(d => d!.Decisions)
            .Include(m => m.Draft)!.ThenInclude(d => d!.Commitments)
            .Include(m => m.Draft)!.ThenInclude(d => d!.Issues)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<Meeting> CreateMeetingAsync(CreateMeetingRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Transcript))
            throw new ArgumentException("Transcript cannot be empty.");
        if (request.Transcript.Length > 200_000)
            throw new ArgumentException("Transcript exceeds maximum size (200KB characters).");

        var meeting = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            MeetingDate = request.MeetingDate.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.MeetingDate, DateTimeKind.Utc)
                : request.MeetingDate.ToUniversalTime(),
            Department = request.Department.Trim(),
            Organizer = request.Organizer.Trim(),
            Source = string.IsNullOrWhiteSpace(request.Source) ? "ManualUpload" : request.Source.Trim(),
            Transcript = request.Transcript.Trim(),
            ScenarioKey = "uploaded",
            WorkflowStatus = MeetingWorkflowStatus.New,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync(ct);
        return meeting;
    }
}

public class KnowledgeRepository : IKnowledgeRepository
{
    private readonly AppDbContext _db;

    public KnowledgeRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<KnowledgeArticle>> ListAsync(string? search = null, bool approvedOnly = false, CancellationToken ct = default)
    {
        var q = _db.KnowledgeArticles.AsNoTracking().AsQueryable();
        if (approvedOnly)
            q = q.Where(k => k.ApprovalStatus == ApprovalStatus.Approved);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(k => k.Title.Contains(s) || k.Description.Contains(s) || k.Department.Contains(s));
        }
        return await q.OrderByDescending(k => k.CreatedAtUtc).ToListAsync(ct);
    }

    public Task<KnowledgeArticle?> GetAsync(Guid id, CancellationToken ct = default)
        => _db.KnowledgeArticles.FirstOrDefaultAsync(k => k.Id == id, ct);

    public async Task<KnowledgeArticle> CreateDraftFromIssueAsync(Guid issueId, string title, string description, string ownerName, CancellationToken ct = default)
    {
        var issue = await _db.Issues.FirstOrDefaultAsync(i => i.Id == issueId, ct)
            ?? throw new InvalidOperationException("Issue not found.");

        if (issue.Status is not (IssueStatus.Resolved or IssueStatus.Closed))
            throw new InvalidOperationException("Only resolved/closed issues can publish knowledge drafts.");

        var article = new KnowledgeArticle
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Description = description.Trim(),
            Department = issue.Department,
            OwnerName = ownerName,
            ApprovalStatus = ApprovalStatus.PendingApproval,
            SourceIssueId = issue.Id,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.KnowledgeArticles.Add(article);
        issue.KnowledgeArticleId = article.Id;
        await _db.SaveChangesAsync(ct);
        return article;
    }

    public async Task<KnowledgeArticle> SetApprovalAsync(Guid id, bool approve, Guid actorUserId, string actorName, CancellationToken ct = default)
    {
        var article = await _db.KnowledgeArticles.FirstOrDefaultAsync(k => k.Id == id, ct)
            ?? throw new InvalidOperationException("Knowledge article not found.");

        article.ApprovalStatus = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        article.ApprovedByUserId = actorUserId;
        article.ApprovedByName = actorName;
        article.ApprovedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return article;
    }
}

public class RegisterRepository : IRegisterRepository
{
    private readonly AppDbContext _db;

    public RegisterRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<DecisionRecord>> ListDecisionsAsync(CancellationToken ct = default)
        => await _db.Decisions.AsNoTracking().Include(d => d.Meeting)
            .OrderByDescending(d => d.DecisionDateUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<CommitmentRecord>> ListCommitmentsAsync(CancellationToken ct = default)
    {
        var items = await _db.Commitments.AsNoTracking()
            .Include(c => c.Meeting)
            .Include(c => c.Decision)
            .Include(c => c.DestinationTask)
            .Include(c => c.StatusHistory)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);

        foreach (var c in items)
            CommitmentStatusHelper.RefreshOverdue(c);

        return items;
    }

    public async Task<IReadOnlyList<IssueRecord>> ListIssuesAsync(CancellationToken ct = default)
        => await _db.Issues.AsNoTracking().OrderByDescending(i => i.CreatedAtUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<ProcessRegisterEntry>> ListProcessesAsync(CancellationToken ct = default)
        => await _db.ProcessRegister.AsNoTracking().OrderBy(p => p.ProcessName).ToListAsync(ct);

    public Task<DecisionRecord?> FindDecisionByIdempotencyAsync(string key, CancellationToken ct = default)
        => _db.Decisions.FirstOrDefaultAsync(d => d.IdempotencyKey == key, ct);

    public Task<CommitmentRecord?> FindCommitmentByIdempotencyAsync(string key, CancellationToken ct = default)
        => _db.Commitments.Include(c => c.DestinationTask)
            .FirstOrDefaultAsync(c => c.IdempotencyKey == key, ct);

    public async Task AddDecisionAsync(DecisionRecord decision, CancellationToken ct = default)
    {
        _db.Decisions.Add(decision);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddCommitmentAsync(CommitmentRecord commitment, CancellationToken ct = default)
    {
        _db.Commitments.Add(commitment);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddIssueAsync(IssueRecord issue, CancellationToken ct = default)
    {
        _db.Issues.Add(issue);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateCommitmentAsync(CommitmentRecord commitment, CancellationToken ct = default)
    {
        _db.Commitments.Update(commitment);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateIssueAsync(IssueRecord issue, CancellationToken ct = default)
    {
        _db.Issues.Update(issue);
        await _db.SaveChangesAsync(ct);
    }
}

public static class CommitmentStatusHelper
{
    public static CommitmentStatus ComputeStatus(CommitmentRecord c, DateTime? utcNow = null)
    {
        if (c.Status is CommitmentStatus.Completed or CommitmentStatus.Cancelled)
            return c.Status;

        var now = utcNow ?? DateTime.UtcNow.Date;
        if (c.DueDate.HasValue && c.DueDate.Value.Date < now)
            return CommitmentStatus.Overdue;

        return c.Status == CommitmentStatus.InProgress ? CommitmentStatus.InProgress : CommitmentStatus.Open;
    }

    public static void RefreshOverdue(CommitmentRecord c)
    {
        var computed = ComputeStatus(c);
        if (computed == CommitmentStatus.Overdue)
            c.Status = CommitmentStatus.Overdue;
    }
}

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _db;

    public AuditLogService(AppDbContext db) => _db = db;

    public async Task RecordAsync(
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
        CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorName = actorName,
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            MeetingId = meetingId,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            Succeeded = succeeded,
            FailureReason = failureReason
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> ListAsync(int take = 200, CancellationToken ct = default)
        => await _db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(take)
            .ToListAsync(ct);
}
