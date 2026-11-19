using Microsoft.Extensions.Configuration;

namespace TheSplashLab.Infrastructure.Integrations;

/// <summary>
/// Placeholder for future Microsoft Graph / SharePoint / Lists integration.
/// Does not invent endpoint behavior, permissions, or tenant access.
/// </summary>
public interface IMicrosoftGraphIntegration
{
    bool IsConfigured { get; }
    string StatusLabel { get; }
    string SetupGuidance { get; }
}

public class MicrosoftGraphIntegrationStub : IMicrosoftGraphIntegration
{
    private readonly IConfiguration _config;

    public MicrosoftGraphIntegrationStub(IConfiguration config)
    {
        _config = config;
    }

    public bool IsConfigured
    {
        get
        {
            var tenant = _config["MicrosoftGraph:TenantId"];
            var client = _config["MicrosoftGraph:ClientId"];
            return !string.IsNullOrWhiteSpace(tenant) && !string.IsNullOrWhiteSpace(client);
        }
    }

    public string StatusLabel => IsConfigured
        ? "Configured but not activated — verify permissions in current Microsoft docs before enabling calls."
        : "Not Connected";

    public string SetupGuidance =>
        "To prepare MODE B Microsoft 365 integration: register an application in the appropriate Microsoft Entra tenant, " +
        "confirm required Microsoft Graph permissions from official Microsoft documentation for meetings/transcripts/SharePoint/Lists, " +
        "complete admin consent where required, and supply TenantId/ClientId/ClientSecret via server-side configuration. " +
        "This POC does not invent Graph permissions or call Graph APIs.";
}

public class LocalTaskDestination : TheSplashLab.Core.Interfaces.ITaskDestination
{
    public string DestinationName => "LocalTaskRepository";
    public bool IsSimulated => true;

    public Task<TheSplashLab.Core.Models.TaskCreateResult> CreateTaskAsync(
        TheSplashLab.Core.Models.TaskCreateRequest request,
        CancellationToken ct = default)
    {
        // Scenario 3: simulate destination failure when requested or title contains marker
        if (request.SimulateFailure ||
            request.Title.Contains("FAIL_DESTINATION", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new TheSplashLab.Core.Models.TaskCreateResult
            {
                Succeeded = false,
                FailureReason = "Simulated task destination failure (MODE A). Retry is safe and idempotent."
            });
        }

        return Task.FromResult(new TheSplashLab.Core.Models.TaskCreateResult
        {
            Succeeded = true,
            ExternalTaskId = $"LOCAL-{request.CommitmentId.ToString()[..8].ToUpperInvariant()}"
        });
    }
}
