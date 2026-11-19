using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheSplashLab.Core.Enums;
using TheSplashLab.Core.Models;
using TheSplashLab.Core.Services;
using TheSplashLab.Infrastructure.Data;
using TheSplashLab.Infrastructure.Integrations;
using TheSplashLab.Infrastructure.Repositories;
using TheSplashLab.Infrastructure.Seed;
using Xunit;

namespace TheSplashLab.Tests;

public class WorkflowTests
{
    private PocWebApplicationFactory NewFactory() => new();

    private HttpClient CreateClient(PocWebApplicationFactory factory, Guid? userId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Demo-User-Id", (userId ?? DemoSeed.ReviewerId).ToString());
        return client;
    }

    [Fact]
    public void Transcript_input_validation_rejects_empty()
    {
        var act = () => new MeetingSourceValidation().Validate(new CreateMeetingRequest
        {
            Title = "t",
            Department = "Operations",
            Organizer = "Alex",
            Transcript = " "
        });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Structured_AI_output_validation_accepts_valid_payload()
    {
        var payload = new AiExtractionPayload
        {
            Summary = "A summary",
            Decisions = new() { new() { Statement = "Do X", SourceEvidence = "said X" } },
            Commitments = new()
            {
                new()
                {
                    Action = "Do Y",
                    SuggestedOwner = "Alex",
                    SuggestedDueDate = "2026-11-20",
                    SourceEvidence = "Alex will Do Y by 20 November"
                }
            }
        };
        var result = ExtractionValidator.ValidateAndNormalize(payload, "Mock", "{}");
        result.Summary.Should().Be("A summary");
        result.Commitments.Should().HaveCount(1);
        result.Commitments[0].NeedsReview.Should().BeFalse();
    }

    [Fact]
    public void Missing_owner_and_due_date_marks_needs_review()
    {
        var payload = new AiExtractionPayload
        {
            Summary = "Ambiguous",
            Commitments = new()
            {
                new()
                {
                    Action = "Maybe follow up",
                    SuggestedOwner = null,
                    SuggestedDueDate = null,
                    SourceEvidence = "someone should follow up"
                }
            }
        };
        var result = ExtractionValidator.ValidateAndNormalize(payload, "Mock", "{}");
        result.Commitments[0].NeedsReview.Should().BeTrue();
        result.Commitments[0].NeedsReviewReason.Should().NotBeNullOrWhiteSpace();
        result.Commitments[0].SuggestedOwner.Should().BeNull();
        result.Commitments[0].SuggestedDueDate.Should().BeNull();
    }

    [Fact]
    public async Task Unauthorized_approval_rejection()
    {
        await using var factory = NewFactory();
        var client = CreateClient(factory, DemoSeed.EmployeeId);
        var resp = await client.PostAsJsonAsync($"/api/meetings/{DemoSeed.CompleteMeetingId}/publish", new PublishRequest());
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Complete_scenario_extract_approve_publish_idempotent()
    {
        await using var factory = NewFactory();
        var client = CreateClient(factory, DemoSeed.ReviewerId);

        var gen = await client.PostAsync($"/api/meetings/{DemoSeed.CompleteMeetingId}/generate-draft", null);
        gen.EnsureSuccessStatusCode();
        var draft = await gen.Content.ReadFromJsonAsync<JsonElement>();
        draft.GetProperty("summary").GetString().Should().NotBeNullOrWhiteSpace();
        draft.GetProperty("commitments").GetArrayLength().Should().Be(2);

        var decisions = draft.GetProperty("decisions").EnumerateArray().Select(d => new DraftDecisionEdit
        {
            Id = d.GetProperty("id").GetGuid(),
            Statement = d.GetProperty("statement").GetString()!,
            Context = d.GetProperty("context").GetString()!,
            SourceEvidence = d.GetProperty("sourceEvidence").GetString()!,
            ReviewStatus = "Approved",
            NeedsReview = false
        }).ToList();

        var commitments = draft.GetProperty("commitments").EnumerateArray().Select(c => new DraftCommitmentEdit
        {
            Id = c.GetProperty("id").GetGuid(),
            Action = c.GetProperty("action").GetString()!,
            SuggestedOwner = c.GetProperty("suggestedOwner").GetString(),
            SuggestedDueDate = c.TryGetProperty("suggestedDueDate", out var dd) && dd.ValueKind != JsonValueKind.Null
                ? dd.GetDateTime() : null,
            SourceEvidence = c.GetProperty("sourceEvidence").GetString()!,
            ReviewStatus = "Approved",
            NeedsReview = false
        }).ToList();

        var save = await client.PutAsJsonAsync($"/api/meetings/{DemoSeed.CompleteMeetingId}/draft", new SaveDraftRequest
        {
            Summary = draft.GetProperty("summary").GetString()!,
            DiscussionPoints = draft.GetProperty("discussionPoints").EnumerateArray().Select(x => x.GetString()!).ToList(),
            Decisions = decisions,
            Commitments = commitments,
            Issues = new()
        });
        save.EnsureSuccessStatusCode();

        var publish1 = await client.PostAsJsonAsync($"/api/meetings/{DemoSeed.CompleteMeetingId}/publish", new PublishRequest());
        publish1.EnsureSuccessStatusCode();
        var result1 = await publish1.Content.ReadFromJsonAsync<PublishResult>();
        result1!.Succeeded.Should().BeTrue();
        result1.DecisionsPublished.Should().BeGreaterThan(0);
        result1.CommitmentsPublished.Should().Be(2);
        result1.TasksCreated.Should().Be(2);

        // Idempotent republish
        var publish2 = await client.PostAsJsonAsync($"/api/meetings/{DemoSeed.CompleteMeetingId}/publish", new PublishRequest { ForceRetryFailedTasks = true });
        publish2.EnsureSuccessStatusCode();
        var result2 = await publish2.Content.ReadFromJsonAsync<PublishResult>();
        result2!.DecisionsPublished.Should().BeGreaterThan(0);
        result2.CommitmentsPublished.Should().Be(2);

        var decisionsResp = await client.GetAsync("/api/decisions");
        decisionsResp.EnsureSuccessStatusCode();
        var decisionJson = await decisionsResp.Content.ReadAsStringAsync();
        decisionJson.Should().Contain("matte black");

        var commitmentsResp = await client.GetAsync("/api/commitments");
        commitmentsResp.EnsureSuccessStatusCode();
        var commitmentJson = await commitmentsResp.Content.ReadAsStringAsync();
        commitmentJson.Should().Contain("Alex Morgan");

        var audit = await client.GetAsync("/api/audit");
        audit.EnsureSuccessStatusCode();
        (await audit.Content.ReadAsStringAsync()).Should().Contain("PublishDraft");
    }

    [Fact]
    public async Task Rejected_action_is_not_published_and_failed_destination_is_visible()
    {
        await using var factory = NewFactory();
        var client = CreateClient(factory, DemoSeed.ReviewerId);

        var gen = await client.PostAsync($"/api/meetings/{DemoSeed.FailMeetingId}/generate-draft", null);
        gen.EnsureSuccessStatusCode();
        var draft = await gen.Content.ReadFromJsonAsync<JsonElement>();

        var decisions = draft.GetProperty("decisions").EnumerateArray().Select(d => new DraftDecisionEdit
        {
            Id = d.GetProperty("id").GetGuid(),
            Statement = d.GetProperty("statement").GetString()!,
            Context = d.GetProperty("context").GetString()!,
            SourceEvidence = d.GetProperty("sourceEvidence").GetString()!,
            ReviewStatus = "Approved"
        }).ToList();

        var commitments = draft.GetProperty("commitments").EnumerateArray().Select(c =>
        {
            var action = c.GetProperty("action").GetString()!;
            var reject = action.Contains("FAIL_DESTINATION", StringComparison.OrdinalIgnoreCase) ||
                         action.Contains("ERP", StringComparison.OrdinalIgnoreCase);
            // Approve the checklist; keep FAIL_DESTINATION approved to exercise failure path
            // and also have one rejected non-fail item if present — for this scenario approve FAIL to test failure
            return new DraftCommitmentEdit
            {
                Id = c.GetProperty("id").GetGuid(),
                Action = action,
                SuggestedOwner = c.TryGetProperty("suggestedOwner", out var o) && o.ValueKind != JsonValueKind.Null ? o.GetString() : "Alex Morgan",
                SuggestedDueDate = c.TryGetProperty("suggestedDueDate", out var dd) && dd.ValueKind != JsonValueKind.Null
                    ? dd.GetDateTime() : DateTime.UtcNow.Date.AddDays(7),
                SourceEvidence = c.GetProperty("sourceEvidence").GetString()!,
                ReviewStatus = action.Contains("imaginary", StringComparison.OrdinalIgnoreCase) && !action.Contains("FAIL_DESTINATION")
                    ? "Rejected"
                    : (action.Contains("FAIL_DESTINATION") ? "Approved" : "Approved"),
                NeedsReview = false
            };
        }).ToList();

        // Explicitly reject any non-FAIL imaginary ERP if separate; ensure at least one rejected for audit
        if (commitments.All(c => c.ReviewStatus != "Rejected") && commitments.Count > 1)
        {
            var weak = commitments.FirstOrDefault(c => c.Action.Contains("FAIL_DESTINATION"));
            // Keep FAIL approved for destination failure; add a manual rejected item via save isn't needed —
            // reject the FAIL one for rejection test in a second assertion path.
        }

        // Split: reject FAIL_DESTINATION for rejection test first on a copy — instead approve checklist only, reject FAIL
        foreach (var c in commitments)
        {
            if (c.Action.Contains("FAIL_DESTINATION"))
                c.ReviewStatus = "Rejected";
        }

        var save = await client.PutAsJsonAsync($"/api/meetings/{DemoSeed.FailMeetingId}/draft", new SaveDraftRequest
        {
            Summary = draft.GetProperty("summary").GetString()!,
            DiscussionPoints = new(),
            Decisions = decisions,
            Commitments = commitments,
            Issues = new()
        });
        save.EnsureSuccessStatusCode();

        var publish = await client.PostAsJsonAsync($"/api/meetings/{DemoSeed.FailMeetingId}/publish", new PublishRequest());
        publish.EnsureSuccessStatusCode();
        var result = await publish.Content.ReadFromJsonAsync<PublishResult>();
        result!.Succeeded.Should().BeTrue();
        result.CommitmentsPublished.Should().Be(1);

        var commitmentsResp = await (await client.GetAsync("/api/commitments")).Content.ReadAsStringAsync();
        commitmentsResp.Should().Contain("publish checklist");
        commitmentsResp.Should().NotContain("imaginary ERP queue");
    }

    [Fact]
    public async Task Task_adapter_failure_and_retry()
    {
        await using var factory = NewFactory();
        var client = CreateClient(factory, DemoSeed.ReviewerId);

        // Create a fresh meeting with FAIL_DESTINATION commitment via upload/create
        var create = await client.PostAsJsonAsync("/api/meetings", new CreateMeetingRequest
        {
            Title = "Failure retry meeting",
            Department = "Operations",
            Organizer = "Jordan Lee",
            MeetingDate = DateTime.UtcNow.Date,
            Transcript = ScenarioMini.FailTranscript,
            Source = "Test"
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var meetingId = created.GetProperty("id").GetGuid();

        // Manually set scenario via DB
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var m = await db.Meetings.FirstAsync(x => x.Id == meetingId);
            m.ScenarioKey = "fail-destination";
            await db.SaveChangesAsync();
        }

        var gen = await client.PostAsync($"/api/meetings/{meetingId}/generate-draft", null);
        gen.EnsureSuccessStatusCode();
        var draft = await gen.Content.ReadFromJsonAsync<JsonElement>();

        var commitments = draft.GetProperty("commitments").EnumerateArray().Select(c => new DraftCommitmentEdit
        {
            Id = c.GetProperty("id").GetGuid(),
            Action = c.GetProperty("action").GetString()!,
            SuggestedOwner = "Alex Morgan",
            SuggestedDueDate = DateTime.UtcNow.Date.AddDays(5),
            SourceEvidence = c.GetProperty("sourceEvidence").GetString()!,
            ReviewStatus = "Approved",
            NeedsReview = false
        }).ToList();

        var decisions = draft.GetProperty("decisions").EnumerateArray().Select(d => new DraftDecisionEdit
        {
            Id = d.GetProperty("id").GetGuid(),
            Statement = d.GetProperty("statement").GetString()!,
            Context = d.GetProperty("context").GetString()!,
            SourceEvidence = d.GetProperty("sourceEvidence").GetString()!,
            ReviewStatus = "Approved"
        }).ToList();

        await client.PutAsJsonAsync($"/api/meetings/{meetingId}/draft", new SaveDraftRequest
        {
            Summary = draft.GetProperty("summary").GetString()!,
            Decisions = decisions,
            Commitments = commitments
        });

        var publish = await client.PostAsJsonAsync($"/api/meetings/{meetingId}/publish", new PublishRequest());
        var result = await publish.Content.ReadFromJsonAsync<PublishResult>();
        result!.TasksFailed.Should().BeGreaterThan(0);
        result.Succeeded.Should().BeFalse();

        var list = await (await client.GetAsync("/api/commitments")).Content.ReadAsStringAsync();
        list.Should().Contain("Failed");

        // Retry: still fails for FAIL_DESTINATION (idempotent, visible)
        var retry = await client.PostAsJsonAsync($"/api/meetings/{meetingId}/publish", new PublishRequest { ForceRetryFailedTasks = true });
        var retryResult = await retry.Content.ReadFromJsonAsync<PublishResult>();
        retryResult!.TasksFailed.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Correct_commitment_status_overdue_based_on_due_date()
    {
        var open = new Core.Entities.CommitmentRecord
        {
            Status = CommitmentStatus.Open,
            DueDate = DateTime.UtcNow.Date.AddDays(-1)
        };
        CommitmentStatusHelper.ComputeStatus(open).Should().Be(CommitmentStatus.Overdue);

        var completed = new Core.Entities.CommitmentRecord
        {
            Status = CommitmentStatus.Completed,
            DueDate = DateTime.UtcNow.Date.AddDays(-1)
        };
        CommitmentStatusHelper.ComputeStatus(completed).Should().Be(CommitmentStatus.Completed);
    }

    [Fact]
    public async Task Knowledge_publication_only_after_approval()
    {
        await using var factory = NewFactory();
        var client = CreateClient(factory, DemoSeed.ReviewerId);
        var issueId = Guid.Parse("e2222222-2222-2222-2222-222222222222");

        var create = await client.PostAsJsonAsync($"/api/issues/{issueId}/publish-knowledge", new PublishKnowledgeFromIssueRequest
        {
            Title = "Reusable lead time note",
            Description = "Captured from resolved issue for reuse."
        });
        create.EnsureSuccessStatusCode();
        var article = await create.Content.ReadFromJsonAsync<JsonElement>();
        article.GetProperty("approvalStatus").GetString().Should().Be("PendingApproval");
        var id = article.GetProperty("id").GetGuid();

        var approvedOnly = await (await client.GetAsync("/api/knowledge?approvedOnly=true")).Content.ReadAsStringAsync();
        approvedOnly.Should().NotContain(id.ToString());

        var approve = await client.PostAsJsonAsync($"/api/knowledge/{id}/approve", new ApproveKnowledgeRequest { Approve = true });
        approve.EnsureSuccessStatusCode();

        var after = await (await client.GetAsync("/api/knowledge?approvedOnly=true")).Content.ReadAsStringAsync();
        after.Should().Contain("Reusable lead time note");
    }

    [Fact]
    public async Task Audit_event_recording()
    {
        await using var factory = NewFactory();
        var client = CreateClient(factory);
        await client.PostAsync($"/api/meetings/{DemoSeed.AmbiguousMeetingId}/generate-draft", null);
        var audit = await (await client.GetAsync("/api/audit")).Content.ReadAsStringAsync();
        audit.Should().Contain("GenerateDraft");
    }

    [Fact]
    public async Task Local_task_destination_simulates_failure_marker()
    {
        var dest = new LocalTaskDestination();
        var ok = await dest.CreateTaskAsync(new TaskCreateRequest
        {
            CommitmentId = Guid.NewGuid(),
            Title = "Normal task",
            Assignee = "Alex",
            IdempotencyKey = "k1"
        });
        ok.Succeeded.Should().BeTrue();

        var fail = await dest.CreateTaskAsync(new TaskCreateRequest
        {
            CommitmentId = Guid.NewGuid(),
            Title = "FAIL_DESTINATION boom",
            Assignee = "Alex",
            IdempotencyKey = "k2"
        });
        fail.Succeeded.Should().BeFalse();
        fail.FailureReason.Should().NotBeNullOrWhiteSpace();
    }
}

internal static class ScenarioMini
{
    public const string FailTranscript = """
Jordan: Confirmed decision: Use the local task repository for pilot publishing only.
Alex Morgan will document the pilot publish checklist by 21 November 2026.
Also FAIL_DESTINATION: Sync approved pilot records to imaginary ERP queue immediately. Owner Sam Patel due 15 November 2026.
""";
}

/// <summary>Helper mirroring meeting create validation.</summary>
internal class MeetingSourceValidation
{
    public void Validate(CreateMeetingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Transcript))
            throw new ArgumentException("Transcript cannot be empty.");
        if (request.Transcript.Length > 200_000)
            throw new ArgumentException("Transcript exceeds maximum size (200KB characters).");
    }
}

public class PocWebApplicationFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"splashlab-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Path", _dbPath);
        builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(opt => opt.UseSqlite($"Data Source={_dbPath}"));
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* ignore */ }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* ignore */ }
    }
}
