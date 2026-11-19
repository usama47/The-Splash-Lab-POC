using System.Text.Json;
using TheSplashLab.Core.Entities;
using TheSplashLab.Core.Interfaces;
using TheSplashLab.Core.Models;
using TheSplashLab.Core.Services;
using TheSplashLab.Infrastructure.Seed;

namespace TheSplashLab.Infrastructure.Ai;

public class MockAiExtractionService : IAiExtractionService
{
    public string ProviderName => "MockAi";
    public bool IsExternal => false;

    public Task<ExtractionResult> ExtractAsync(Meeting meeting, CancellationToken ct = default)
    {
        var payload = meeting.ScenarioKey switch
        {
            "complete" => CompletePayload(),
            "ambiguous" => AmbiguousPayload(),
            "fail-destination" => FailDestinationPayload(),
            _ => HeuristicPayload(meeting)
        };

        var raw = JsonSerializer.Serialize(payload);
        var result = ExtractionValidator.ValidateAndNormalize(payload, ProviderName, raw);
        return Task.FromResult(result);
    }

    private static AiExtractionPayload CompletePayload() => new()
    {
        Summary = "Operations and Sales aligned on Riverside sample and quotation follow-up. Matte black samples will be sent and the quotation will be revised.",
        DiscussionPoints = new List<string>
        {
            "Riverside architect requested matte black finish samples.",
            "Revised quotation needed for updated finish pricing.",
            "No open blockers from operations."
        },
        Decisions = new List<AiDecisionPayload>
        {
            new()
            {
                Statement = "Send matte black finish samples for the Riverside project.",
                Context = "Architect requested matte black finish samples and a revised quotation.",
                SourceEvidence = "Decision confirmed: Send matte black finish samples for the Riverside project.",
                NeedsReview = false
            }
        },
        Commitments = new List<AiCommitmentPayload>
        {
            new()
            {
                Action = "Prepare and courier the matte black sample pack to the Riverside project architect.",
                SuggestedOwner = "Alex Morgan",
                SuggestedDueDate = "2026-11-17",
                SourceEvidence = "Owner is Alex Morgan. Due date is 17 November 2026.",
                NeedsReview = false
            },
            new()
            {
                Action = "Update the quotation with revised finish pricing and send it to the architect.",
                SuggestedOwner = "Priya Shah",
                SuggestedDueDate = "2026-11-14",
                SourceEvidence = "Owner is Priya Shah. Due date is 14 November 2026.",
                NeedsReview = false
            }
        },
        OpenIssues = new List<AiIssuePayload>()
    };

    private static AiExtractionPayload AmbiguousPayload() => new()
    {
        Summary = "Product and operations discussed a possible brass comparison board and an unresolved lead-time question. Ownership and deadlines were not assigned.",
        DiscussionPoints = new List<string>
        {
            "US architect preferred brushed brass tone.",
            "Comparison board for brushed brass versus antique brass was discussed.",
            "Lead-time escalation to US warehouse left unresolved."
        },
        Decisions = new List<AiDecisionPayload>
        {
            new()
            {
                Statement = "Park the comparison board idea until ownership is clarified.",
                Context = "Team discussed a possible comparison board but deferred commitment.",
                SourceEvidence = "Let's park the comparison board idea until we clarify ownership.",
                NeedsReview = false
            }
        },
        Commitments = new List<AiCommitmentPayload>
        {
            new()
            {
                Action = "Follow up with a brushed brass versus antique brass comparison board.",
                SuggestedOwner = null,
                SuggestedDueDate = null,
                SourceEvidence = "Someone should probably follow up with a comparison board, but we did not assign an owner or a deadline in this call.",
                NeedsReview = true,
                NeedsReviewReason = "Owner and due date not stated in transcript."
            }
        },
        OpenIssues = new List<AiIssuePayload>
        {
            new()
            {
                Title = "US sample lead-time clarification",
                Description = "Whether to escalate lead-time question to the US warehouse remains unresolved.",
                SourceEvidence = "We also wondered whether to escalate the lead-time question to the US warehouse, but that was left unresolved.",
                NeedsReview = true
            }
        }
    };

    private static AiExtractionPayload FailDestinationPayload() => new()
    {
        Summary = "Pilot team confirmed local task repository usage and captured a publish checklist commitment, plus a weak ERP suggestion that should be rejected.",
        DiscussionPoints = new List<string>
        {
            "Use local task repository for pilot publishing only.",
            "Document pilot publish checklist.",
            "Reject invented ERP write actions."
        },
        Decisions = new List<AiDecisionPayload>
        {
            new()
            {
                Statement = "Use the local task repository for pilot publishing only.",
                Context = "Keeps the POC destination simulated and controllable.",
                SourceEvidence = "Confirmed decision: Use the local task repository for pilot publishing only.",
                NeedsReview = false
            }
        },
        Commitments = new List<AiCommitmentPayload>
        {
            new()
            {
                Action = "Document the pilot publish checklist.",
                SuggestedOwner = "Alex Morgan",
                SuggestedDueDate = "2026-11-21",
                SourceEvidence = "Alex Morgan will document the pilot publish checklist by 21 November 2026.",
                NeedsReview = false
            },
            new()
            {
                Action = "FAIL_DESTINATION: Sync approved pilot records to imaginary ERP queue immediately.",
                SuggestedOwner = "Sam Patel",
                SuggestedDueDate = "2026-11-15",
                SourceEvidence = "vague suggestion to sync everything to an imaginary ERP queue immediately",
                NeedsReview = true,
                NeedsReviewReason = "Action invents an ERP write outside POC scope."
            }
        },
        OpenIssues = new List<AiIssuePayload>()
    };

    private static AiExtractionPayload HeuristicPayload(Meeting meeting)
    {
        var transcript = meeting.Transcript ?? string.Empty;
        var lines = transcript.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var summary = lines.Length == 0
            ? "Draft summary requires reviewer input."
            : string.Join(" ", lines.Take(3));

        var commitments = new List<AiCommitmentPayload>();
        foreach (var line in lines.Where(l =>
                     l.Contains("will ", StringComparison.OrdinalIgnoreCase) ||
                     l.Contains("owner", StringComparison.OrdinalIgnoreCase)))
        {
            commitments.Add(new AiCommitmentPayload
            {
                Action = line.Length > 180 ? line[..180] : line,
                SuggestedOwner = null,
                SuggestedDueDate = null,
                SourceEvidence = line,
                NeedsReview = true,
                NeedsReviewReason = "Heuristic extraction; owner/due date need reviewer confirmation."
            });
        }

        if (commitments.Count == 0)
        {
            commitments.Add(new AiCommitmentPayload
            {
                Action = "Review transcript and capture any missing commitments.",
                NeedsReview = true,
                NeedsReviewReason = "No explicit commitments detected.",
                SourceEvidence = summary
            });
        }

        return new AiExtractionPayload
        {
            Summary = summary.Length > 400 ? summary[..400] : summary,
            DiscussionPoints = lines.Take(5).ToList(),
            Decisions = new List<AiDecisionPayload>
            {
                new()
                {
                    Statement = "Reviewer to confirm whether any formal decision was made.",
                    Context = "Heuristic extraction for uploaded/pasted transcript.",
                    SourceEvidence = summary,
                    NeedsReview = true
                }
            },
            Commitments = commitments.Take(5).ToList(),
            OpenIssues = new List<AiIssuePayload>()
        };
    }
}
