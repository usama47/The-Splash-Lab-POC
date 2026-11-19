using TheSplashLab.Core.Entities;
using TheSplashLab.Core.Enums;
using TheSplashLab.Infrastructure.Data;

namespace TheSplashLab.Infrastructure.Seed;

public static class DemoSeed
{
    public static readonly Guid EmployeeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ReviewerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static readonly Guid CompleteMeetingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid AmbiguousMeetingId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid FailMeetingId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid PriorMeetingId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    public static readonly Guid PriorDecisionId = Guid.Parse("de111111-1111-1111-1111-111111111111");
    public static readonly Guid PriorCommitmentOpenId = Guid.Parse("c0111111-1111-1111-1111-111111111111");
    public static readonly Guid PriorCommitmentDoneId = Guid.Parse("c0222222-2222-2222-2222-222222222222");

    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (!db.Users.Any())
        {
            db.Users.AddRange(
                new DemoUser
                {
                    Id = EmployeeId,
                    DisplayName = "Alex Morgan",
                    Email = "alex.morgan@splashlab.demo",
                    Department = "Operations",
                    Role = UserRole.Employee
                },
                new DemoUser
                {
                    Id = ReviewerId,
                    DisplayName = "Jordan Lee",
                    Email = "jordan.lee@splashlab.demo",
                    Department = "Operations",
                    Role = UserRole.Reviewer
                },
                new DemoUser
                {
                    Id = AdminId,
                    DisplayName = "Sam Patel",
                    Email = "sam.patel@splashlab.demo",
                    Department = "IT",
                    Role = UserRole.Administrator
                });
        }

        if (!db.Meetings.Any())
        {
            db.Meetings.AddRange(
                new Meeting
                {
                    Id = CompleteMeetingId,
                    Title = "UK Sample Coordination — Quotation Follow-up",
                    MeetingDate = new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc),
                    Department = "Operations",
                    Organizer = "Jordan Lee",
                    Source = "Simulated Teams Transcript",
                    ScenarioKey = "complete",
                    WorkflowStatus = MeetingWorkflowStatus.New,
                    Transcript = ScenarioTranscripts.CompleteMeeting
                },
                new Meeting
                {
                    Id = AmbiguousMeetingId,
                    Title = "Product Sample Review — Architect Feedback",
                    MeetingDate = new DateTime(2026, 11, 12, 14, 0, 0, DateTimeKind.Utc),
                    Department = "Operations",
                    Organizer = "Alex Morgan",
                    Source = "Simulated Teams Transcript",
                    ScenarioKey = "ambiguous",
                    WorkflowStatus = MeetingWorkflowStatus.New,
                    Transcript = ScenarioTranscripts.AmbiguousMeeting
                },
                new Meeting
                {
                    Id = FailMeetingId,
                    Title = "Project Kickoff — Destination Adapter Stress Case",
                    MeetingDate = new DateTime(2026, 11, 14, 9, 30, 0, DateTimeKind.Utc),
                    Department = "Operations",
                    Organizer = "Jordan Lee",
                    Source = "Simulated Teams Transcript",
                    ScenarioKey = "fail-destination",
                    WorkflowStatus = MeetingWorkflowStatus.New,
                    Transcript = ScenarioTranscripts.FailDestinationMeeting
                },
                new Meeting
                {
                    Id = PriorMeetingId,
                    Title = "US Warehouse Sync — Finish Availability",
                    MeetingDate = DateTime.UtcNow.Date.AddDays(-10),
                    Department = "Operations",
                    Organizer = "Jordan Lee",
                    Source = "Simulated Teams Transcript",
                    ScenarioKey = "prior-published",
                    WorkflowStatus = MeetingWorkflowStatus.Published,
                    Transcript = ScenarioTranscripts.PriorPublishedMeeting,
                    UpdatedAtUtc = DateTime.UtcNow.AddDays(-9)
                });
        }

        if (!db.Decisions.Any())
        {
            db.Decisions.Add(new DecisionRecord
            {
                Id = PriorDecisionId,
                Statement = "Publish a shared US/UK finish availability note for Operations and Sales.",
                Context = "Agreed in the US warehouse sync to stop answering finish questions from personal inboxes.",
                MeetingId = PriorMeetingId,
                ApproverName = "Jordan Lee",
                ApproverUserId = ReviewerId,
                DecisionDateUtc = DateTime.UtcNow.AddDays(-9),
                IsApproved = true,
                Department = "Operations",
                IdempotencyKey = $"decision:{PriorMeetingId}:seed-prior"
            });
        }

        if (!db.Commitments.Any())
        {
            var openDue = DateTime.UtcNow.Date.AddDays(4);
            var doneDue = DateTime.UtcNow.Date.AddDays(-2);

            db.Commitments.AddRange(
                new CommitmentRecord
                {
                    Id = PriorCommitmentOpenId,
                    Action = "Draft the shared finish availability note and circulate to Sales.",
                    OwnerName = "Alex Morgan",
                    OwnerUserId = EmployeeId,
                    DueDate = openDue,
                    Status = CommitmentStatus.InProgress,
                    MeetingId = PriorMeetingId,
                    DecisionId = PriorDecisionId,
                    Department = "Operations",
                    IdempotencyKey = $"commitment:{PriorMeetingId}:seed-open",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-9),
                    UpdatedAtUtc = DateTime.UtcNow.AddDays(-1),
                    StatusHistory =
                    {
                        new CommitmentStatusHistory
                        {
                            Id = Guid.NewGuid(),
                            CommitmentId = PriorCommitmentOpenId,
                            FromStatus = CommitmentStatus.Open,
                            ToStatus = CommitmentStatus.InProgress,
                            ChangedBy = "Alex Morgan",
                            Note = "Started drafting after warehouse confirmation",
                            ChangedAtUtc = DateTime.UtcNow.AddDays(-1)
                        }
                    },
                    DestinationTask = new DestinationTask
                    {
                        Id = Guid.NewGuid(),
                        CommitmentId = PriorCommitmentOpenId,
                        ExternalTaskId = "LOCAL-PRIOR01",
                        Title = "Draft the shared finish availability note and circulate to Sales.",
                        Assignee = "Alex Morgan",
                        DueDate = openDue,
                        Status = TaskDestinationStatus.Created,
                        DestinationName = "LocalTaskRepository",
                        IdempotencyKey = $"task:{PriorCommitmentOpenId}",
                        CreatedAtUtc = DateTime.UtcNow.AddDays(-9)
                    }
                },
                new CommitmentRecord
                {
                    Id = PriorCommitmentDoneId,
                    Action = "Confirm antique brass restock date with the US warehouse lead.",
                    OwnerName = "Sam Patel",
                    OwnerUserId = AdminId,
                    DueDate = doneDue,
                    Status = CommitmentStatus.Completed,
                    MeetingId = PriorMeetingId,
                    DecisionId = PriorDecisionId,
                    Department = "Operations",
                    IdempotencyKey = $"commitment:{PriorMeetingId}:seed-done",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-9),
                    UpdatedAtUtc = DateTime.UtcNow.AddDays(-3),
                    StatusHistory =
                    {
                        new CommitmentStatusHistory
                        {
                            Id = Guid.NewGuid(),
                            CommitmentId = PriorCommitmentDoneId,
                            FromStatus = CommitmentStatus.Open,
                            ToStatus = CommitmentStatus.Completed,
                            ChangedBy = "Sam Patel",
                            Note = "Restock confirmed for next Friday",
                            ChangedAtUtc = DateTime.UtcNow.AddDays(-3)
                        }
                    },
                    DestinationTask = new DestinationTask
                    {
                        Id = Guid.NewGuid(),
                        CommitmentId = PriorCommitmentDoneId,
                        ExternalTaskId = "LOCAL-PRIOR02",
                        Title = "Confirm antique brass restock date with the US warehouse lead.",
                        Assignee = "Sam Patel",
                        DueDate = doneDue,
                        Status = TaskDestinationStatus.Created,
                        DestinationName = "LocalTaskRepository",
                        IdempotencyKey = $"task:{PriorCommitmentDoneId}",
                        CreatedAtUtc = DateTime.UtcNow.AddDays(-9)
                    }
                });
        }

        if (!db.ProcessRegister.Any())
        {
            db.ProcessRegister.AddRange(
                new ProcessRegisterEntry
                {
                    Id = Guid.Parse("d1111111-1111-1111-1111-111111111111"),
                    ProcessName = "Post-meeting action capture",
                    Department = "Operations",
                    Region = ProcessRegion.Global,
                    OwnerName = "Jordan Lee",
                    Frequency = "Per meeting",
                    EstimatedHoursPerWeek = 6.0m,
                    SystemsUsed = "Teams, Email, Spreadsheets",
                    PainScore = 4,
                    Classification = ProcessClassification.Automate,
                    OpportunityForImprovement = "Extract decisions/commitments with human review gate.",
                    CurrentStatus = "Pilot"
                },
                new ProcessRegisterEntry
                {
                    Id = Guid.Parse("d2222222-2222-2222-2222-222222222222"),
                    ProcessName = "Quotation follow-up chasing",
                    Department = "Sales",
                    Region = ProcessRegion.UK,
                    OwnerName = "Alex Morgan",
                    Frequency = "Daily",
                    EstimatedHoursPerWeek = 8.5m,
                    SystemsUsed = "HubSpot (future), Email",
                    PainScore = 5,
                    Classification = ProcessClassification.Accelerate,
                    OpportunityForImprovement = "Link approved commitments to CRM tasks when HubSpot is connected.",
                    CurrentStatus = "Mapped"
                },
                new ProcessRegisterEntry
                {
                    Id = Guid.Parse("d3333333-3333-3333-3333-333333333333"),
                    ProcessName = "Sample request coordination",
                    Department = "Operations",
                    Region = ProcessRegion.US,
                    OwnerName = "Sam Patel",
                    Frequency = "Weekly",
                    EstimatedHoursPerWeek = 4.0m,
                    SystemsUsed = "Shopify (future), Email",
                    PainScore = 3,
                    Classification = ProcessClassification.Elevate,
                    OpportunityForImprovement = "Reuse approved knowledge articles for recurring sample FAQs.",
                    CurrentStatus = "Mapped"
                },
                new ProcessRegisterEntry
                {
                    Id = Guid.Parse("d4444444-4444-4444-4444-444444444444"),
                    ProcessName = "Project change decision logging",
                    Department = "Projects",
                    Region = ProcessRegion.Global,
                    OwnerName = "Jordan Lee",
                    Frequency = "Weekly",
                    EstimatedHoursPerWeek = 3.5m,
                    SystemsUsed = "SharePoint (future), Email",
                    PainScore = 3,
                    Classification = ProcessClassification.Automate,
                    OpportunityForImprovement = "Publish approved decisions to a shared register.",
                    CurrentStatus = "Mapped"
                },
                new ProcessRegisterEntry
                {
                    Id = Guid.Parse("d5555555-5555-5555-5555-555555555555"),
                    ProcessName = "Architect sample pack assembly",
                    Department = "Operations",
                    Region = ProcessRegion.UK,
                    OwnerName = "Alex Morgan",
                    Frequency = "Daily",
                    EstimatedHoursPerWeek = 5.0m,
                    SystemsUsed = "Email, Warehouse sheet",
                    PainScore = 4,
                    Classification = ProcessClassification.Accelerate,
                    OpportunityForImprovement = "Standardize pack contents from approved knowledge articles.",
                    CurrentStatus = "Mapped"
                },
                new ProcessRegisterEntry
                {
                    Id = Guid.Parse("d6666666-6666-6666-6666-666666666666"),
                    ProcessName = "US / UK finish availability checks",
                    Department = "Product",
                    Region = ProcessRegion.Global,
                    OwnerName = "Chris Nguyen",
                    Frequency = "Weekly",
                    EstimatedHoursPerWeek = 2.5m,
                    SystemsUsed = "Email, Product catalog",
                    PainScore = 2,
                    Classification = ProcessClassification.Elevate,
                    OpportunityForImprovement = "Publish a reusable finishes matrix into Knowledge.",
                    CurrentStatus = "Mapped"
                });
        }

        if (!db.Issues.Any())
        {
            db.Issues.AddRange(
                new IssueRecord
                {
                    Id = Guid.Parse("e1111111-1111-1111-1111-111111111111"),
                    Title = "Inconsistent owner assignment after sales handoff",
                    Description = "Action owners are sometimes left blank when notes move from sales to operations.",
                    Department = "Operations",
                    OwnerName = "Jordan Lee",
                    Status = IssueStatus.Open,
                    Priority = IssuePriority.High
                },
                new IssueRecord
                {
                    Id = Guid.Parse("e2222222-2222-2222-2222-222222222222"),
                    Title = "Sample lead times not recorded in one place",
                    Description = "Teams ask repeatedly for standard sample lead times for architects.",
                    Department = "Operations",
                    OwnerName = "Alex Morgan",
                    Status = IssueStatus.Resolved,
                    Priority = IssuePriority.Medium,
                    Resolution = "Documented standard lead times: UK 5 business days, US 7 business days for standard finishes.",
                    ResolvedAtUtc = DateTime.UtcNow.AddDays(-3)
                },
                new IssueRecord
                {
                    Id = Guid.Parse("e3333333-3333-3333-3333-333333333333"),
                    Title = "Matte black finish stock confirmation lag",
                    Description = "Sales waits on ad-hoc warehouse replies before confirming matte black availability to architects.",
                    Department = "Sales",
                    OwnerName = "Priya Shah",
                    Status = IssueStatus.InProgress,
                    Priority = IssuePriority.Medium
                },
                new IssueRecord
                {
                    Id = Guid.Parse("e4444444-4444-4444-4444-444444444444"),
                    Title = "Meeting actions tracked in personal notebooks",
                    Description = "Project coordinators capture commitments differently across UK and US teams, causing missed follow-ups.",
                    Department = "Projects",
                    OwnerName = "Jordan Lee",
                    Status = IssueStatus.Open,
                    Priority = IssuePriority.Critical
                });
        }

        if (!db.KnowledgeArticles.Any())
        {
            db.KnowledgeArticles.AddRange(
                new KnowledgeArticle
                {
                    Id = Guid.Parse("f1111111-1111-1111-1111-111111111111"),
                    Title = "Standard sample lead times (architect requests)",
                    Description = "UK: 5 business days for standard finishes. US: 7 business days. Expedite requires Operations Manager approval.",
                    Department = "Operations",
                    OwnerName = "Alex Morgan",
                    ApprovalStatus = ApprovalStatus.Approved,
                    ApprovedByName = "Jordan Lee",
                    ApprovedByUserId = ReviewerId,
                    ApprovedAtUtc = DateTime.UtcNow.AddDays(-2),
                    SourceIssueId = Guid.Parse("e2222222-2222-2222-2222-222222222222")
                },
                new KnowledgeArticle
                {
                    Id = Guid.Parse("f2222222-2222-2222-2222-222222222222"),
                    Title = "Meeting-to-task review checklist (pilot)",
                    Description = "1) Confirm decision wording. 2) Require owner + due date on every commitment. 3) Reject invented system writes. 4) Publish only after reviewer approval.",
                    Department = "Operations",
                    OwnerName = "Jordan Lee",
                    ApprovalStatus = ApprovalStatus.Approved,
                    ApprovedByName = "Jordan Lee",
                    ApprovedByUserId = ReviewerId,
                    ApprovedAtUtc = DateTime.UtcNow.AddDays(-1)
                },
                new KnowledgeArticle
                {
                    Id = Guid.Parse("f3333333-3333-3333-3333-333333333333"),
                    Title = "Draft: Brass comparison board guidance",
                    Description = "Proposed guidance for brushed vs antique brass boards. Waiting for Product owner confirmation before approval.",
                    Department = "Product",
                    OwnerName = "Chris Nguyen",
                    ApprovalStatus = ApprovalStatus.PendingApproval
                });
        }

        if (!db.IntegrationSettings.Any())
        {
            db.IntegrationSettings.AddRange(
                new IntegrationSetting
                {
                    Id = Guid.NewGuid(),
                    Name = "Meeting Source (Teams Transcript)",
                    Category = "Microsoft 365",
                    Status = IntegrationStatus.Simulated,
                    ModeLabel = "MODE A — Simulated",
                    Description = "Local seeded transcripts stand in for Teams meeting retrieval.",
                    SetupNotes = "Future: Microsoft Graph meeting/transcript APIs after tenant consent is verified from official docs.",
                    IsSimulated = true
                },
                new IntegrationSetting
                {
                    Id = Guid.NewGuid(),
                    Name = "AI Extraction (Claude)",
                    Category = "AI",
                    Status = IntegrationStatus.Simulated,
                    ModeLabel = "MODE A — Mock AI (default)",
                    Description = "Deterministic mock extractor. Optional Claude when ANTHROPIC_API_KEY is set and Ai:Provider=Claude.",
                    SetupNotes = "Set ANTHROPIC_API_KEY and Ai:Provider=Claude to enable MODE B AI. External AI remains off by default.",
                    IsSimulated = true
                },
                new IntegrationSetting
                {
                    Id = Guid.NewGuid(),
                    Name = "Task Destination",
                    Category = "Tasks",
                    Status = IntegrationStatus.Simulated,
                    ModeLabel = "MODE A — Local Task Repository",
                    Description = "Tasks are written to the local SQLite destination adapter.",
                    SetupNotes = "Future destination adapters (e.g. Microsoft Lists) can replace ITaskDestination without changing publish workflow.",
                    IsSimulated = true
                },
                new IntegrationSetting
                {
                    Id = Guid.NewGuid(),
                    Name = "Microsoft Graph / SharePoint / Lists",
                    Category = "Microsoft 365",
                    Status = IntegrationStatus.NotConnected,
                    ModeLabel = "Not Connected",
                    Description = "Interfaces and configuration placeholders only. No invented Graph behavior.",
                    SetupNotes = "Verify required Graph permissions and app registration from current Microsoft documentation before enabling.",
                    IsSimulated = false
                },
                new IntegrationSetting
                {
                    Id = Guid.NewGuid(),
                    Name = "Acumatica",
                    Category = "ERP",
                    Status = IntegrationStatus.NotConnected,
                    ModeLabel = "Not Connected",
                    Description = "Future operational data source. Not part of this POC vertical slice.",
                    SetupNotes = "Out of scope for the pilot. Show status only.",
                    IsSimulated = false
                },
                new IntegrationSetting
                {
                    Id = Guid.NewGuid(),
                    Name = "HubSpot",
                    Category = "CRM",
                    Status = IntegrationStatus.NotConnected,
                    ModeLabel = "Not Connected",
                    Description = "Future CRM task/contact sync. Not connected in this POC.",
                    SetupNotes = "Out of scope for the pilot. Show status only.",
                    IsSimulated = false
                },
                new IntegrationSetting
                {
                    Id = Guid.NewGuid(),
                    Name = "Shopify",
                    Category = "Commerce",
                    Status = IntegrationStatus.NotConnected,
                    ModeLabel = "Not Connected",
                    Description = "Future commerce data. Not connected in this POC.",
                    SetupNotes = "Out of scope for the pilot. Show status only.",
                    IsSimulated = false
                });
        }

        if (!db.AuditLogs.Any())
        {
            db.AuditLogs.AddRange(
                new AuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    OccurredAtUtc = DateTime.UtcNow.AddDays(-9),
                    ActorName = "Jordan Lee",
                    ActorUserId = ReviewerId,
                    Action = "PublishDraft",
                    EntityType = "Meeting",
                    EntityId = PriorMeetingId.ToString(),
                    MeetingId = PriorMeetingId,
                    Succeeded = true,
                    AfterValues = "Published prior US warehouse sync decision and commitments."
                },
                new AuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    OccurredAtUtc = DateTime.UtcNow.AddDays(-3),
                    ActorName = "Sam Patel",
                    ActorUserId = AdminId,
                    Action = "UpdateCommitmentStatus",
                    EntityType = "Commitment",
                    EntityId = PriorCommitmentDoneId.ToString(),
                    MeetingId = PriorMeetingId,
                    BeforeValues = "InProgress",
                    AfterValues = "Completed",
                    Succeeded = true
                },
                new AuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    OccurredAtUtc = DateTime.UtcNow.AddMinutes(-5),
                    ActorName = "System",
                    Action = "SeedDatabase",
                    EntityType = "Database",
                    EntityId = "demo",
                    Succeeded = true,
                    AfterValues = "Seeded users, meetings, processes, issues, knowledge, prior published records, integrations."
                });
        }

        await db.SaveChangesAsync();
    }
}

internal static class ScenarioTranscripts
{
    public const string CompleteMeeting = """
Meeting: UK Sample Coordination — Quotation Follow-up
Date: 10 November 2026
Attendees: Jordan Lee (Operations), Alex Morgan (Operations), Priya Shah (Sales)

Jordan: Thanks everyone. We need to close the loop on the Riverside architect quotation.

Priya: The architect asked for matte black finish samples and a revised quotation by next week.

Jordan: Agreed. We will proceed with sending matte black finish samples to the Riverside project architect.
Decision confirmed: Send matte black finish samples for the Riverside project.

Alex: I will prepare the sample pack and courier it. Owner is Alex Morgan. Due date is 17 November 2026.

Priya: I will update the quotation with the revised finish pricing and send it to the architect. Owner is Priya Shah. Due date is 14 November 2026.

Jordan: Perfect. Two commitments: Alex owns sample courier by 17 November 2026; Priya owns revised quotation by 14 November 2026.

Alex: No open blockers from operations side.
""";

    public const string AmbiguousMeeting = """
Meeting: Product Sample Review — Architect Feedback
Date: 12 November 2026
Attendees: Alex Morgan, Jordan Lee, Chris Nguyen (Product)

Alex: The US architect liked the brushed brass tone but was unsure about lead times.

Chris: We discussed possibly preparing a comparison board for brushed brass versus antique brass.

Jordan: Someone should probably follow up with a comparison board, but we did not assign an owner or a deadline in this call.

Alex: We also wondered whether to escalate the lead-time question to the US warehouse, but that was left unresolved.

Chris: Let's park the comparison board idea until we clarify ownership.
""";

    public const string FailDestinationMeeting = """
Meeting: Project Kickoff — Destination Adapter Stress Case
Date: 14 November 2026
Attendees: Jordan Lee, Alex Morgan, Sam Patel

Jordan: For the demo stress case we will approve one valid commitment and intentionally reject a weak AI suggestion.

Alex: Confirmed decision: Use the local task repository for pilot publishing only.

Jordan: Commitment one — Alex Morgan will document the pilot publish checklist by 21 November 2026.

Sam: There was also a vague suggestion to sync everything to an imaginary ERP queue immediately, which we should reject.

Jordan: Correct. Reject any action that invents an ERP write. We will also exercise a simulated task destination failure for the retry path using the special marker FAIL_DESTINATION in the commitment title when testing.
""";

    public const string PriorPublishedMeeting = """
Meeting: US Warehouse Sync — Finish Availability
Attendees: Jordan Lee, Alex Morgan, Sam Patel

Jordan: Decision confirmed — publish a shared US/UK finish availability note for Operations and Sales.

Alex: I will draft the shared finish availability note and circulate it to Sales.

Sam: I will confirm the antique brass restock date with the US warehouse lead.
""";
}
