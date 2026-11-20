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

    private static async Task<JsonElement> GenerateAsync(HttpClient client, Guid meetingId)
    {
        var gen = await client.PostAsync($"/api/meetings/{meetingId}/generate-draft", null);
        gen.EnsureSuccessStatusCode();
        return await gen.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<PublishResult> PublishAsync(HttpClient client, Guid meetingId, PublishRequest request)
    {
        var resp = await client.PostAsJsonAsync($"/api/meetings/{meetingId}/publish", request);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PublishResult>())!;
    }

    /// <summary>Builds the on-screen draft state from a draft payload with every item approved.</summary>
    private static SaveDraftRequest ApproveAll(JsonElement draft, Action<DraftCommitmentEdit>? editCommitment = null)
    {
        var request = new SaveDraftRequest
        {
            Summary = draft.GetProperty("summary").GetString()!,
            DiscussionPoints = new(),
            Decisions = draft.GetProperty("decisions").EnumerateArray().Select(d => new DraftDecisionEdit
            {
                Id = d.GetProperty("id").GetGuid(),
                Statement = d.GetProperty("statement").GetString()!,
                Context = d.GetProperty("context").GetString()!,
                SourceEvidence = d.GetProperty("sourceEvidence").GetString()!,
                ReviewStatus = "Approved"
            }).ToList(),
            Commitments = draft.GetProperty("commitments").EnumerateArray().Select(c => new DraftCommitmentEdit
            {
                Id = c.GetProperty("id").GetGuid(),
                Action = c.GetProperty("action").GetString()!,
                SuggestedOwner = c.GetProperty("suggestedOwner").GetString(),
                SuggestedDueDate = c.GetProperty("suggestedDueDate").GetDateTime(),
                SourceEvidence = c.GetProperty("sourceEvidence").GetString()!,
                ReviewStatus = "Approved"
            }).ToList()
        };

        if (editCommitment is not null)
            foreach (var c in request.Commitments) editCommitment(c);
        return request;
    }

    private static AppDbContext OpenDb(PocWebApplicationFactory factory)
        => factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    [Fact]
    public async Task Publish_applies_inline_edits_and_task_uses_edited_owner_and_wording()
    {
        await using var factory = NewFactory();
        var client = CreateClient(factory);
        var draft = await GenerateAsync(client, DemoSeed.CompleteMeetingId);

        var onScreen = ApproveAll(draft, c =>
        {
            if (!c.Action.StartsWith("Prepare and courier")) return;
            c.Action = "Courier the matte black sample pack by express post.";
            c.SuggestedOwner = "Taylor Quinn";
        });

        // No separate save: the on-screen edits travel with the publish request.
        var result = await PublishAsync(client, DemoSeed.CompleteMeetingId, new PublishRequest
        {
            ExpectedVersion = draft.GetProperty("version").GetString(),
            Draft = onScreen
        });

        result.Succeeded.Should().BeTrue();
        result.TasksNewlyCreated.Should().Be(2);

        var db = OpenDb(factory);
        var tasks = await db.DestinationTasks
            .Where(t => t.Commitment.MeetingId == DemoSeed.CompleteMeetingId)
            .ToListAsync();
        tasks.Should().HaveCount(2);
        tasks.Should().Contain(t => t.Title == "Courier the matte black sample pack by express post." && t.Assignee == "Taylor Quinn");
        tasks.Should().NotContain(t => t.Title.StartsWith("Prepare and courier"));
    }

    [Fact]
    public async Task Rejecting_a_previously_approved_action_creates_no_task_for_it()
    {
        await using var factory = NewFactory();
        var client = CreateClient(factory);
        var draft = await GenerateAsync(client, DemoSeed.CompleteMeetingId);

        // The server holds both actions as Approved after an earlier save.
        var saved = await client.PutAsJsonAsync($"/api/meetings/{DemoSeed.CompleteMeetingId}/draft", ApproveAll(draft));
        saved.EnsureSuccessStatusCode();
        var savedDraft = await saved.Content.ReadFromJsonAsync<JsonElement>();

        // The reviewer then rejects the quotation action on screen and publishes without saving again.
        var onScreen = ApproveAll(savedDraft, c =>
        {
            if (c.Action.StartsWith("Update the quotation")) c.ReviewStatus = "Rejected";
        });
        var result = await PublishAsync(client, DemoSeed.CompleteMeetingId, new PublishRequest
        {
            ExpectedVersion = savedDraft.GetProperty("version").GetString(),
            Draft = onScreen
        });

        result.CommitmentsPublished.Should().Be(1);
        result.TasksNewlyCreated.Should().Be(1);

        var db = OpenDb(factory);
        var commitments = await db.Commitments.Where(c => c.MeetingId == DemoSeed.CompleteMeetingId).ToListAsync();
        commitments.Should().ContainSingle();
        commitments.Should().NotContain(c => c.Action.StartsWith("Update the quotation"));
        (await db.DestinationTasks.CountAsync(t => t.Commitment.MeetingId == DemoSeed.CompleteMeetingId)).Should().Be(1);
    }

    [Fact]
    public async Task Publish_with_stale_version_returns_conflict_and_publishes_nothing()
    {
        await using var factory = NewFactory();
        var client = CreateClient(factory);
        var draft = await GenerateAsync(client, DemoSeed.CompleteMeetingId);
        var staleVersion = draft.GetProperty("version").GetString();

        // Another reviewer saves a change after this reviewer opened the draft.
        var otherReviewer = ApproveAll(draft);
        otherReviewer.Summary = "Edited by another reviewer.";
        (await client.PutAsJsonAsync($"/api/meetings/{DemoSeed.CompleteMeetingId}/draft", otherReviewer)).EnsureSuccessStatusCode();

        var publish = await client.PostAsJsonAsync($"/api/meetings/{DemoSeed.CompleteMeetingId}/publish", new PublishRequest
        {
            ExpectedVersion = staleVersion,
            Draft = ApproveAll(draft)
        });

        publish.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await publish.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("currentDraft").GetProperty("summary").GetString().Should().Be("Edited by another reviewer.");

        var db = OpenDb(factory);
        (await db.Commitments.CountAsync(c => c.MeetingId == DemoSeed.CompleteMeetingId)).Should().Be(0);
    }

    [Theory]
    [InlineData("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "2026-11-17", "2026-11-14")]
    [InlineData("cccccccc-cccc-cccc-cccc-cccccccccccc", "2026-11-21", "2026-11-24")]
    public async Task Seeded_evidence_and_dates_match_transcript_and_regeneration_is_stable(string meetingId, string dueA, string dueB)
    {
        await using var factory = NewFactory();
        var client = CreateClient(factory);
        var id = Guid.Parse(meetingId);

        var meeting = await client.GetFromJsonAsync<JsonElement>($"/api/meetings/{id}");
        var transcript = meeting.GetProperty("transcript").GetString()!;

        foreach (var draft in new[] { await GenerateAsync(client, id), await GenerateAsync(client, id) })
        {
            var commitments = draft.GetProperty("commitments").EnumerateArray().ToList();
            commitments
                .Select(c => c.GetProperty("suggestedDueDate").GetDateTime().ToString("yyyy-MM-dd"))
                .Should().BeEquivalentTo(new[] { dueA, dueB });

            foreach (var item in commitments.Concat(draft.GetProperty("decisions").EnumerateArray()))
                transcript.Should().Contain(item.GetProperty("sourceEvidence").GetString());
        }
    }

    [Fact]
    public async Task Scenario3_failed_task_recovers_on_retry_without_duplicating_tasks()
    {
        await using var factory = NewFactory();
        var client = CreateClient(factory);
        var meetingId = DemoSeed.FailMeetingId;
        var draft = await GenerateAsync(client, meetingId);
        draft.GetProperty("commitments").GetArrayLength().Should().Be(2);

        var first = await PublishAsync(client, meetingId, new PublishRequest
        {
            ExpectedVersion = draft.GetProperty("version").GetString(),
            Draft = ApproveAll(draft)
        });

        first.Succeeded.Should().BeFalse();
        first.TasksNewlyCreated.Should().Be(1);
        first.TasksAlreadyExisting.Should().Be(0);
        first.TasksFailed.Should().Be(1);
        var okTask = first.Tasks.Single(t => t.Status == "Created");
        var failedTask = first.Tasks.Single(t => t.Status == "Failed");
        okTask.IsNew.Should().BeTrue();
        failedTask.Action.Should().Be(ScenarioTranscripts.FailOnceAction);
        failedTask.FailureReason.Should().NotBeNullOrWhiteSpace();

        var retry = await PublishAsync(client, meetingId, new PublishRequest { ForceRetryFailedTasks = true });

        retry.Succeeded.Should().BeTrue();
        retry.TasksNewlyCreated.Should().Be(1);
        retry.TasksAlreadyExisting.Should().Be(1);
        retry.TasksFailed.Should().Be(0);
        retry.Tasks.Should().OnlyContain(t => t.Status == "Created");
        retry.Tasks.Single(t => t.CommitmentId == okTask.CommitmentId).TaskId.Should().Be(okTask.TaskId);
        retry.Tasks.Single(t => t.CommitmentId == failedTask.CommitmentId).TaskId.Should().Be(failedTask.TaskId);

        var again = await PublishAsync(client, meetingId, new PublishRequest { ForceRetryFailedTasks = true });

        again.TasksNewlyCreated.Should().Be(0);
        again.TasksAlreadyExisting.Should().Be(2);
        again.TasksFailed.Should().Be(0);

        var db = OpenDb(factory);
        var tasks = await db.DestinationTasks.Where(t => t.Commitment.MeetingId == meetingId).ToListAsync();
        tasks.Should().HaveCount(2);
        tasks.Should().OnlyContain(t => t.Status == TaskDestinationStatus.Created);

        (await client.GetStringAsync("/api/audit")).Should().Contain("RetryFailedTasks");
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
