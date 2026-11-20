using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TheSplashLab.Core.Enums;
using TheSplashLab.Core.Interfaces;
using TheSplashLab.Core.Models;
using TheSplashLab.Infrastructure.Data;
using TheSplashLab.Infrastructure.Integrations;

namespace TheSplashLab.Api.Controllers;

[ApiController]
[Route("api/integrations")]
public class IntegrationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAiExtractionService _ai;
    private readonly IMicrosoftGraphIntegration _graph;
    private readonly IAuditLogService _audit;
    private readonly ICurrentUserAccessor _user;

    public IntegrationsController(
        AppDbContext db, IAiExtractionService ai, IMicrosoftGraphIntegration graph,
        IAuditLogService audit, ICurrentUserAccessor user)
    {
        _db = db;
        _ai = ai;
        _graph = graph;
        _audit = audit;
        _user = user;
    }

    public record ConnectAttemptRequest(string Name);

    // Name goes in the body because integration names contain slashes. Only the click is logged; no credentials.
    [HttpPost("connect-attempt")]
    public async Task<IActionResult> LogConnectAttempt([FromBody] ConnectAttemptRequest req, CancellationToken ct)
    {
        var setting = await _db.IntegrationSettings.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Name == req.Name, ct);
        if (setting is null) return NotFound();

        await _audit.RecordAsync(_user.DisplayName, _user.UserId, "ConnectIntegration", "Integration",
            setting.Name, null, null, $"Connect started for {setting.Name} ({setting.Status})", true, null, ct);
        return Ok(new { logged = true });
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _db.IntegrationSettings.AsNoTracking()
            .OrderBy(i => i.Category).ThenBy(i => i.Name).ToListAsync(ct);

        foreach (var ai in items.Where(i => i.Name.Contains("Claude", StringComparison.OrdinalIgnoreCase)))
        {
            ai.ModeLabel = _ai.IsExternal ? "MODE B — Claude" : "MODE A — Mock AI (default)";
            ai.Status = _ai.IsExternal ? IntegrationStatus.Connected : IntegrationStatus.Simulated;
            ai.IsSimulated = !_ai.IsExternal;
        }

        foreach (var g in items.Where(i => i.Name.Contains("Graph", StringComparison.OrdinalIgnoreCase)))
        {
            g.ModeLabel = _graph.StatusLabel;
            g.Status = _graph.IsConfigured ? IntegrationStatus.Misconfigured : IntegrationStatus.NotConnected;
            g.SetupNotes = _graph.SetupGuidance;
        }

        return Ok(items.Select(i => new IntegrationStatusDto
        {
            Name = i.Name,
            Category = i.Category,
            Status = i.Status.ToString(),
            ModeLabel = i.ModeLabel,
            Description = i.Description,
            SetupNotes = i.SetupNotes,
            IsSimulated = i.IsSimulated
        }));
    }
}
