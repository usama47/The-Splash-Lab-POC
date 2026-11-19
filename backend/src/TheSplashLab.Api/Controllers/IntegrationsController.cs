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

    public IntegrationsController(AppDbContext db, IAiExtractionService ai, IMicrosoftGraphIntegration graph)
    {
        _db = db;
        _ai = ai;
        _graph = graph;
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
