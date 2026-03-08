using Apex.Core.Interfaces;
using Apex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Apex.Infrastructure.Services;

public class AgencyGate : IAgencyGate
{
    private readonly ApexDbContext _db;
    private readonly AgencyGateOptions _opts;

    public AgencyGate(ApexDbContext db, AgencyGateOptions opts)
    {
        _db = db;
        _opts = opts;
    }

    public async Task<AgencyReadiness> CheckReadinessAsync()
    {
        var result = new AgencyReadiness
        {
            SuggestionsRequired = _opts.MinSuggestions,
            FeedbackRateRequired = _opts.MinFeedbackRate,
            SuppressionsRequired = _opts.MinAntiPatternSuppressions,
            ConsolidatedSessionsRequired = _opts.MinConsolidatedSessions
        };

        // 1. Suggestions
        result.SuggestionsLogged = await _db.Suggestions.CountAsync();
        if (result.SuggestionsLogged < _opts.MinSuggestions)
            result.BlockingReasons.Add(
                $"Need {_opts.MinSuggestions} suggestions, have {result.SuggestionsLogged}");
        if (_opts.MinSuggestions < AgencyGateOptions.RecommendedSuggestions && result.SuggestionsLogged < AgencyGateOptions.RecommendedSuggestions)
            result.Warnings.Add(
                $"Suggestions threshold ({_opts.MinSuggestions}) is below recommended ({AgencyGateOptions.RecommendedSuggestions}). Agency may act on limited data.");

        // 2. Feedback rate
        var totalSuggestions = result.SuggestionsLogged;
        var withOutcomes = await _db.Outcomes
            .Select(o => o.SuggestionId)
            .Distinct()
            .CountAsync();
        result.ExplicitFeedbackRate = totalSuggestions > 0
            ? (double)withOutcomes / totalSuggestions
            : 0;
        if (result.ExplicitFeedbackRate < _opts.MinFeedbackRate)
            result.BlockingReasons.Add(
                $"Need {_opts.MinFeedbackRate:P0} feedback rate, have {result.ExplicitFeedbackRate:P0}");
        if (_opts.MinFeedbackRate < AgencyGateOptions.RecommendedFeedbackRate && result.ExplicitFeedbackRate < AgencyGateOptions.RecommendedFeedbackRate)
            result.Warnings.Add(
                $"Feedback rate threshold ({_opts.MinFeedbackRate:P0}) is below recommended ({AgencyGateOptions.RecommendedFeedbackRate:P0}). Quality of autonomous actions may be lower.");

        // 3. Anti-pattern suppressions
        result.AntiPatternSuppressions = await _db.AntiPatterns.CountAsync();
        if (result.AntiPatternSuppressions < _opts.MinAntiPatternSuppressions)
            result.BlockingReasons.Add(
                $"Need {_opts.MinAntiPatternSuppressions} anti-pattern suppressions, have {result.AntiPatternSuppressions}");
        if (_opts.MinAntiPatternSuppressions < AgencyGateOptions.RecommendedSuppressions && result.AntiPatternSuppressions < AgencyGateOptions.RecommendedSuppressions)
            result.Warnings.Add(
                $"Anti-pattern threshold ({_opts.MinAntiPatternSuppressions}) is below recommended ({AgencyGateOptions.RecommendedSuppressions}).");

        // 4. Consolidated sessions
        result.ConsolidatedSessions = await _db.Sessions
            .CountAsync(s => s.IsConsolidated);
        if (result.ConsolidatedSessions < _opts.MinConsolidatedSessions)
            result.BlockingReasons.Add(
                $"Need {_opts.MinConsolidatedSessions} consolidated sessions, have {result.ConsolidatedSessions}");
        if (_opts.MinConsolidatedSessions < AgencyGateOptions.RecommendedConsolidatedSessions && result.ConsolidatedSessions < AgencyGateOptions.RecommendedConsolidatedSessions)
            result.Warnings.Add(
                $"Consolidated sessions threshold ({_opts.MinConsolidatedSessions}) is below recommended ({AgencyGateOptions.RecommendedConsolidatedSessions}). Agency has limited learning history.");

        result.IsReady = result.BlockingReasons.Count == 0;
        return result;
    }
}
