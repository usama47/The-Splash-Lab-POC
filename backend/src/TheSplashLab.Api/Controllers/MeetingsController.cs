using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheSplashLab.Core.Interfaces;
using TheSplashLab.Core.Models;
using TheSplashLab.Infrastructure.Services;

namespace TheSplashLab.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MeetingsController : ControllerBase
{
    private readonly IMeetingSource _meetings;
    private readonly MeetingWorkflowService _workflow;
    private readonly IAuditLogService _audit;
    private readonly ICurrentUserAccessor _user;

    public MeetingsController(
        IMeetingSource meetings,
        MeetingWorkflowService workflow,
        IAuditLogService audit,
        ICurrentUserAccessor user)
    {
        _meetings = meetings;
        _workflow = workflow;
        _audit = audit;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? department = "Operations", CancellationToken ct = default)
    {
        var items = await _meetings.GetMeetingsAsync(department, ct);
        return Ok(items.Select(m => new
        {
            m.Id,
            m.Title,
            m.MeetingDate,
            m.Department,
            m.Organizer,
            m.Source,
            m.ScenarioKey,
            WorkflowStatus = m.WorkflowStatus.ToString(),
            m.CreatedAtUtc
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
    {
        var m = await _meetings.GetMeetingAsync(id, ct);
        if (m is null) return NotFound();

        return Ok(new
        {
            m.Id,
            m.Title,
            m.MeetingDate,
            m.Department,
            m.Organizer,
            m.Source,
            m.ScenarioKey,
            WorkflowStatus = m.WorkflowStatus.ToString(),
            m.Transcript,
            Draft = m.Draft is null ? null : MapDraft(m.Draft)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMeetingRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var meeting = await _meetings.CreateMeetingAsync(request, ct);
            await _audit.RecordAsync(_user.DisplayName, _user.UserId, "CreateMeeting", "Meeting", meeting.Id.ToString(),
                meeting.Id, null, meeting.Title, true, null, ct);
            return CreatedAtAction(nameof(Get), new { id = meeting.Id }, new { meeting.Id, meeting.Title });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("upload")]
    [RequestSizeLimit(200_000)]
    public async Task<IActionResult> Upload([FromForm] string title, [FromForm] string department, [FromForm] string organizer, [FromForm] DateTime? meetingDate, IFormFile? file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Empty file rejected." });
        if (file.Length > 200_000)
            return BadRequest(new { error = "File exceeds 200KB limit." });
        if (!string.Equals(Path.GetExtension(file.FileName), ".txt", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only .txt transcripts are accepted." });

        using var reader = new StreamReader(file.OpenReadStream());
        var transcript = await reader.ReadToEndAsync(ct);
        var meeting = await _meetings.CreateMeetingAsync(new CreateMeetingRequest
        {
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title,
            Department = string.IsNullOrWhiteSpace(department) ? "Operations" : department,
            Organizer = string.IsNullOrWhiteSpace(organizer) ? _user.DisplayName : organizer,
            MeetingDate = meetingDate ?? DateTime.UtcNow.Date,
            Transcript = transcript,
            Source = "FileUpload"
        }, ct);

        return CreatedAtAction(nameof(Get), new { id = meeting.Id }, new { meeting.Id, meeting.Title });
    }

    [HttpPost("{id:guid}/generate-draft")]
    public async Task<IActionResult> GenerateDraft(Guid id, CancellationToken ct = default)
    {
        try
        {
            var draft = await _workflow.GenerateDraftAsync(id, ct);
            return Ok(MapDraft(draft));
        }
        catch (Exception ex) when (ex is InvalidOperationException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/draft")]
    [Authorize(Policy = "Reviewer")]
    public async Task<IActionResult> SaveDraft(Guid id, [FromBody] SaveDraftRequest request, CancellationToken ct = default)
    {
        try
        {
            var draft = await _workflow.SaveDraftAsync(id, request, ct);
            return Ok(MapDraft(draft));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = "Reviewer")]
    public async Task<IActionResult> Publish(Guid id, [FromBody] PublishRequest? request, CancellationToken ct = default)
    {
        try
        {
            var result = await _workflow.PublishAsync(id, request ?? new PublishRequest(), ct);
            if (!result.Succeeded && result.DecisionsPublished == 0 && result.CommitmentsPublished == 0)
                return BadRequest(result);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static object MapDraft(Core.Entities.MeetingDraft d) => new
    {
        d.Id,
        d.MeetingId,
        d.Summary,
        DiscussionPoints = JsonSerializer.Deserialize<List<string>>(d.DiscussionPointsJson) ?? new(),
        d.ProviderName,
        d.IsPublished,
        d.CreatedAtUtc,
        d.UpdatedAtUtc,
        d.ApprovedAtUtc,
        Decisions = d.Decisions.Select(x => new
        {
            x.Id,
            x.Statement,
            x.Context,
            x.SourceEvidence,
            ReviewStatus = x.ReviewStatus.ToString(),
            x.NeedsReview,
            x.PublishedDecisionId
        }),
        Commitments = d.Commitments.Select(x => new
        {
            x.Id,
            x.Action,
            x.SuggestedOwner,
            x.SuggestedDueDate,
            x.SourceEvidence,
            ReviewStatus = x.ReviewStatus.ToString(),
            x.NeedsReview,
            x.NeedsReviewReason,
            x.PublishedCommitmentId
        }),
        Issues = d.Issues.Select(x => new
        {
            x.Id,
            x.Title,
            x.Description,
            x.SourceEvidence,
            ReviewStatus = x.ReviewStatus.ToString(),
            x.NeedsReview,
            x.PublishedIssueId
        })
    };
}
