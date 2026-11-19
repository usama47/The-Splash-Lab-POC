using System.Text.Json;
using TheSplashLab.Core.Models;

namespace TheSplashLab.Core.Services;

public static class ExtractionValidator
{
    public static ExtractionResult ValidateAndNormalize(AiExtractionPayload payload, string providerName, string rawJson)
    {
        if (payload is null)
            throw new InvalidOperationException("AI extraction payload was null.");

        var result = new ExtractionResult
        {
            Summary = (payload.Summary ?? string.Empty).Trim(),
            DiscussionPoints = (payload.DiscussionPoints ?? new())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToList(),
            ProviderName = providerName,
            RawJson = rawJson
        };

        if (string.IsNullOrWhiteSpace(result.Summary))
            throw new InvalidOperationException("AI extraction missing required summary.");

        foreach (var d in payload.Decisions ?? new())
        {
            if (string.IsNullOrWhiteSpace(d.Statement))
                continue;

            result.Decisions.Add(new ExtractedDecision
            {
                Statement = d.Statement.Trim(),
                Context = (d.Context ?? string.Empty).Trim(),
                SourceEvidence = (d.SourceEvidence ?? string.Empty).Trim(),
                NeedsReview = d.NeedsReview || string.IsNullOrWhiteSpace(d.SourceEvidence)
            });
        }

        foreach (var c in payload.Commitments ?? new())
        {
            if (string.IsNullOrWhiteSpace(c.Action))
                continue;

            DateTime? due = null;
            var needsReview = c.NeedsReview;
            var reason = c.NeedsReviewReason ?? string.Empty;

            if (string.IsNullOrWhiteSpace(c.SuggestedOwner))
            {
                needsReview = true;
                if (string.IsNullOrWhiteSpace(reason))
                    reason = "Owner not stated in transcript.";
            }

            if (!string.IsNullOrWhiteSpace(c.SuggestedDueDate))
            {
                if (DateTime.TryParse(c.SuggestedDueDate, out var parsed))
                    due = parsed.Date;
                else
                {
                    needsReview = true;
                    reason = string.IsNullOrWhiteSpace(reason)
                        ? "Due date could not be parsed."
                        : reason + " Due date could not be parsed.";
                }
            }
            else
            {
                needsReview = true;
                if (string.IsNullOrWhiteSpace(reason))
                    reason = "Due date not stated in transcript.";
                else if (!reason.Contains("due date", StringComparison.OrdinalIgnoreCase))
                    reason += " Due date not stated in transcript.";
            }

            result.Commitments.Add(new ExtractedCommitment
            {
                Action = c.Action.Trim(),
                SuggestedOwner = string.IsNullOrWhiteSpace(c.SuggestedOwner) ? null : c.SuggestedOwner.Trim(),
                SuggestedDueDate = due,
                SourceEvidence = (c.SourceEvidence ?? string.Empty).Trim(),
                NeedsReview = needsReview,
                NeedsReviewReason = reason.Trim()
            });
        }

        foreach (var i in payload.OpenIssues ?? new())
        {
            if (string.IsNullOrWhiteSpace(i.Title))
                continue;

            result.OpenIssues.Add(new ExtractedIssue
            {
                Title = i.Title.Trim(),
                Description = (i.Description ?? string.Empty).Trim(),
                SourceEvidence = (i.SourceEvidence ?? string.Empty).Trim(),
                NeedsReview = i.NeedsReview
            });
        }

        return result;
    }

    public static AiExtractionPayload ParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("AI returned empty JSON.");

        try
        {
            var payload = JsonSerializer.Deserialize<AiExtractionPayload>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (payload is null)
                throw new InvalidOperationException("AI JSON deserialized to null.");

            return payload;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("AI returned malformed JSON.", ex);
        }
    }
}
