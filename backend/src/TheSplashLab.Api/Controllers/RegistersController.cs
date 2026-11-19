using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TheSplashLab.Core.Interfaces;
using TheSplashLab.Core.Models;
using TheSplashLab.Infrastructure.Data;
using TheSplashLab.Infrastructure.Repositories;
using TheSplashLab.Infrastructure.Services;

namespace TheSplashLab.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly MeetingWorkflowService _workflow;
    public DashboardController(MeetingWorkflowService workflow) => _workflow = workflow;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await _workflow.GetDashboardAsync(ct));
}

[ApiController]
[Route("api/decisions")]
public class DecisionsController : ControllerBase
{
    private readonly IRegisterRepository _registers;
    public DecisionsController(IRegisterRepository registers) => _registers = registers;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _registers.ListDecisionsAsync(ct);
        return Ok(items.Select(d => new
        {
            d.Id,
            d.Statement,
            d.Context,
            d.MeetingId,
            MeetingTitle = d.Meeting?.Title,
            d.ApproverName,
            d.DecisionDateUtc,
            d.IsApproved,
            d.Department
        }));
    }
}

[ApiController]
[Route("api/commitments")]
public class CommitmentsController : ControllerBase
{
    private readonly IRegisterRepository _registers;
    private readonly MeetingWorkflowService _workflow;

    public CommitmentsController(IRegisterRepository registers, MeetingWorkflowService workflow)
    {
        _registers = registers;
        _workflow = workflow;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _registers.ListCommitmentsAsync(ct);
        return Ok(items.Select(c =>
        {
            var status = CommitmentStatusHelper.ComputeStatus(c);
            return new
            {
                c.Id,
                c.Action,
                c.OwnerName,
                c.DueDate,
                Status = status.ToString(),
                c.MeetingId,
                MeetingTitle = c.Meeting?.Title,
                c.DecisionId,
                DecisionStatement = c.Decision?.Statement,
                Task = c.DestinationTask is null ? null : new
                {
                    c.DestinationTask.ExternalTaskId,
                    Status = c.DestinationTask.Status.ToString(),
                    c.DestinationTask.FailureReason,
                    c.DestinationTask.DestinationName
                },
                History = c.StatusHistory.OrderBy(h => h.ChangedAtUtc).Select(h => new
                {
                    From = h.FromStatus.ToString(),
                    To = h.ToStatus.ToString(),
                    h.ChangedBy,
                    h.Note,
                    h.ChangedAtUtc
                })
            };
        }));
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "Reviewer")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateCommitmentStatusRequest request, CancellationToken ct)
    {
        try
        {
            var c = await _workflow.UpdateCommitmentStatusAsync(id, request, ct);
            return Ok(new { c.Id, Status = c.Status.ToString() });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/issues")]
public class IssuesController : ControllerBase
{
    private readonly IRegisterRepository _registers;
    private readonly IKnowledgeRepository _knowledge;
    private readonly ICurrentUserAccessor _user;
    private readonly IAuditLogService _audit;

    public IssuesController(IRegisterRepository registers, IKnowledgeRepository knowledge, ICurrentUserAccessor user, IAuditLogService audit)
    {
        _registers = registers;
        _knowledge = knowledge;
        _user = user;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _registers.ListIssuesAsync(ct);
        return Ok(items.Select(i => new
        {
            i.Id,
            i.Title,
            i.Description,
            i.Department,
            i.OwnerName,
            Status = i.Status.ToString(),
            Priority = i.Priority.ToString(),
            i.Resolution,
            i.KnowledgeArticleId,
            i.CreatedAtUtc,
            i.ResolvedAtUtc
        }));
    }

    [HttpPost("{id:guid}/publish-knowledge")]
    [Authorize(Policy = "Reviewer")]
    public async Task<IActionResult> PublishKnowledge(Guid id, [FromBody] PublishKnowledgeFromIssueRequest request, CancellationToken ct)
    {
        try
        {
            var article = await _knowledge.CreateDraftFromIssueAsync(id, request.Title, request.Description, _user.DisplayName, ct);
            await _audit.RecordAsync(_user.DisplayName, _user.UserId, "CreateKnowledgeDraftFromIssue", "KnowledgeArticle",
                article.Id.ToString(), null, null, article.Title, true, null, ct);
            return Ok(new
            {
                article.Id,
                article.Title,
                ApprovalStatus = article.ApprovalStatus.ToString(),
                article.SourceIssueId
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/knowledge")]
public class KnowledgeController : ControllerBase
{
    private readonly IKnowledgeRepository _knowledge;
    private readonly ICurrentUserAccessor _user;
    private readonly IAuditLogService _audit;

    public KnowledgeController(IKnowledgeRepository knowledge, ICurrentUserAccessor user, IAuditLogService audit)
    {
        _knowledge = knowledge;
        _user = user;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] bool approvedOnly = false, CancellationToken ct = default)
    {
        var items = await _knowledge.ListAsync(search, approvedOnly, ct);
        return Ok(items.Select(k => new
        {
            k.Id,
            k.Title,
            k.Description,
            k.Department,
            k.OwnerName,
            ApprovalStatus = k.ApprovalStatus.ToString(),
            k.SourceIssueId,
            k.ApprovedByName,
            k.CreatedAtUtc,
            k.ApprovedAtUtc
        }));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "Reviewer")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveKnowledgeRequest request, CancellationToken ct)
    {
        try
        {
            var article = await _knowledge.SetApprovalAsync(id, request.Approve, _user.UserId, _user.DisplayName, ct);
            await _audit.RecordAsync(_user.DisplayName, _user.UserId,
                request.Approve ? "ApproveKnowledge" : "RejectKnowledge",
                "KnowledgeArticle", article.Id.ToString(), null, null, article.ApprovalStatus.ToString(), true, null, ct);
            return Ok(new { article.Id, ApprovalStatus = article.ApprovalStatus.ToString() });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/processes")]
public class ProcessesController : ControllerBase
{
    private readonly IRegisterRepository _registers;
    public ProcessesController(IRegisterRepository registers) => _registers = registers;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _registers.ListProcessesAsync(ct);
        return Ok(items.Select(p => new
        {
            p.Id,
            p.ProcessName,
            p.Department,
            Region = p.Region.ToString(),
            p.OwnerName,
            p.Frequency,
            p.EstimatedHoursPerWeek,
            p.SystemsUsed,
            p.PainScore,
            Classification = p.Classification.ToString(),
            p.OpportunityForImprovement,
            p.CurrentStatus
        }));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var items = await _registers.ListProcessesAsync(ct);
        var summary = items.GroupBy(p => p.Classification)
            .Select(g => new ProcessSummaryDto
            {
                Classification = g.Key.ToString(),
                TotalHoursPerWeek = g.Sum(x => x.EstimatedHoursPerWeek),
                ProcessCount = g.Count()
            })
            .OrderBy(s => s.Classification)
            .ToList();
        return Ok(summary);
    }
}

[ApiController]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly IAuditLogService _audit;
    public AuditController(IAuditLogService audit) => _audit = audit;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int take = 200, CancellationToken ct = default)
    {
        var items = await _audit.ListAsync(take, ct);
        return Ok(items.Select(a => new
        {
            a.Id,
            a.OccurredAtUtc,
            a.ActorName,
            a.Action,
            a.EntityType,
            a.EntityId,
            a.MeetingId,
            a.BeforeValues,
            a.AfterValues,
            a.Succeeded,
            a.FailureReason
        }));
    }
}

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _user;

    public UsersController(AppDbContext db, ICurrentUserAccessor user)
    {
        _db = db;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking().OrderBy(u => u.DisplayName).ToListAsync(ct);
        return Ok(new
        {
            Current = new { _user.UserId, _user.DisplayName, _user.Email, _user.Role },
            DemoModeNotice = "Identity switching is for local demo only. This is not Microsoft Entra authentication.",
            Users = users.Select(u => new
            {
                u.Id,
                u.DisplayName,
                u.Email,
                u.Department,
                Role = u.Role.ToString()
            })
        });
    }
}
