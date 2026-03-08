using System.Security.Cryptography;
using Apex.Core.Enums;
using Apex.Core.Interfaces;
using Apex.Core.Models;
using Apex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Apex.Infrastructure.Experiments;

public class ExperimentService : IExperimentService
{
    private readonly ApexDbContext _db;
    private readonly IMessageService _messages;
    private readonly ISuggestionService _suggestions;
    private readonly IOutcomeService _outcomes;

    private static readonly Dictionary<ContextTier, ContextFormat> Defaults = new()
    {
        [ContextTier.P1] = ContextFormat.Prose,
        [ContextTier.P2] = ContextFormat.Prose,
        [ContextTier.P3] = ContextFormat.Prose,
        [ContextTier.P4] = ContextFormat.Prose,
        [ContextTier.P5] = ContextFormat.Prose
    };

    public ExperimentService(ApexDbContext db, IMessageService messages, ISuggestionService suggestions, IOutcomeService outcomes)
    {
        _db = db;
        _messages = messages;
        _suggestions = suggestions;
        _outcomes = outcomes;
    }

    public async Task<Experiment> CreateAsync(string name, ContextTier tier, int targetSessions = 60)
    {
        var experiment = new Experiment
        {
            Name = name,
            Tier = tier,
            TargetSessions = targetSessions,
            StartedAt = DateTime.Now
        };
        _db.Experiments.Add(experiment);
        await _db.SaveChangesAsync();
        return experiment;
    }

    public async Task<SessionFormatPlan> AssignFormatsAsync(int sessionId)
    {
        var plan = new SessionFormatPlan { SessionId = sessionId };

        var active = await _db.Experiments
            .Where(e => e.Status == ExperimentStatus.Active)
            .Select(e => new { e.ExperimentId, e.Tier })
            .ToListAsync();

        var concluded = await _db.Experiments
            .Where(e => e.Status == ExperimentStatus.Concluded && e.WinnerFormat != null)
            .ToDictionaryAsync(e => e.Tier, e => e.WinnerFormat!.Value);

        foreach (var tier in Enum.GetValues<ContextTier>())
        {
            var experiment = active.FirstOrDefault(e => e.Tier == tier);

            if (experiment != null)
            {
                var format = await PickBalancedFormatAsync(experiment.ExperimentId);
                plan.TierFormats[tier] = format;

                _db.ExperimentAssignments.Add(new ExperimentAssignment
                {
                    ExperimentId = experiment.ExperimentId,
                    SessionId = sessionId,
                    Format = format,
                    Tier = tier,
                    AssignedAt = DateTime.Now
                });
            }
            else if (concluded.TryGetValue(tier, out var winner))
            {
                plan.TierFormats[tier] = winner;
            }
            else
            {
                plan.TierFormats[tier] = Defaults[tier];
            }
        }

        await _db.SaveChangesAsync();
        return plan;
    }

    private async Task<ContextFormat> PickBalancedFormatAsync(int experimentId)
    {
        var counts = await _db.ExperimentAssignments
            .Where(a => a.ExperimentId == experimentId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                AclCount = g.Count(a => a.Format == ContextFormat.ACL),
                ProseCount = g.Count(a => a.Format == ContextFormat.Prose)
            })
            .FirstOrDefaultAsync();

        var aclCount = counts?.AclCount ?? 0;
        var proseCount = counts?.ProseCount ?? 0;
        var total = aclCount + proseCount;

        if (total < 4) return total % 2 == 0 ? ContextFormat.ACL : ContextFormat.Prose;

        var aclRatio = (double)aclCount / total;
        if (aclRatio > 0.6) return ContextFormat.Prose;
        if (aclRatio < 0.4) return ContextFormat.ACL;

        return RandomNumberGenerator.GetInt32(2) == 0 ? ContextFormat.ACL : ContextFormat.Prose;
    }

    public async Task RecordTokenUsageAsync(int sessionId, List<ContextSegment> segments)
    {
        var assignments = await _db.ExperimentAssignments
            .Where(a => a.SessionId == sessionId)
            .ToListAsync();

        foreach (var assignment in assignments)
        {
            var segment = segments.FirstOrDefault(s => s.Tier == assignment.Tier);
            if (segment != null)
            {
                assignment.TokensUsed = segment.TokensUsed;
                assignment.TokenBudget = segment.TokenBudget;
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<ExperimentTokenSummary>> GetTokenResultsAsync()
    {
        return await _db.ExperimentAssignments
            .Where(a => a.TokensUsed != null)
            .Join(_db.Experiments.Where(e => e.Status == ExperimentStatus.Active),
                a => a.ExperimentId, e => e.ExperimentId, (a, e) => a)
            .GroupBy(a => new { a.Tier, a.Format })
            .Select(g => new ExperimentTokenSummary
            {
                Tier = g.Key.Tier,
                Format = g.Key.Format,
                Sessions = g.Count(),
                AvgTokensUsed = g.Average(a => (double)a.TokensUsed!.Value),
                MinTokensUsed = g.Min(a => a.TokensUsed!.Value),
                MaxTokensUsed = g.Max(a => a.TokensUsed!.Value),
                TokenBudget = g.Max(a => a.TokenBudget ?? 0),
                AvgUtilizationPct = g.Average(a => a.TokenBudget > 0
                    ? (double)a.TokensUsed!.Value / a.TokenBudget!.Value * 100.0
                    : 0)
            })
            .OrderBy(s => s.Tier)
            .ThenBy(s => s.Format)
            .ToListAsync();
    }

    public async Task CollectMetricsAsync(int sessionId)
    {
        var assignments = await _db.ExperimentAssignments
            .Where(a => a.SessionId == sessionId)
            .Select(a => new { a.AssignmentId, a.Tier })
            .ToListAsync();

        if (assignments.Count == 0) return;

        var suggestionCount = await _suggestions.GetCountBySessionAsync(sessionId);
        var suggestionsApplied = await _suggestions.GetAppliedCountBySessionAsync(sessionId);
        var (worked, failed) = await _outcomes.GetCountsBySessionAsync(sessionId);
        var effortSaved = await _outcomes.GetEffortSavedBySessionAsync(sessionId);
        var corrections = await _messages.GetCorrectionCountAsync(sessionId);
        var reExplanations = await _messages.GetRepeatExplanationCountAsync(sessionId);

        var messages = _db.Messages.Where(m => m.SessionId == sessionId);
        var tokensIn = await messages.Where(m => m.Role == MessageRole.User).SumAsync(m => m.TokenCount ?? 0);
        var tokensOut = await messages.Where(m => m.Role == MessageRole.Assistant).SumAsync(m => m.TokenCount ?? 0);

        var timestamps = await messages
            .OrderBy(m => m.Timestamp)
            .Select(m => m.Timestamp)
            .ToListAsync();

        double? durationMin = timestamps.Count >= 2
            ? (timestamps.Last() - timestamps.First()).TotalMinutes
            : null;

        foreach (var assignment in assignments)
        {
            _db.ExperimentMetrics.Add(new ExperimentMetrics
            {
                AssignmentId = assignment.AssignmentId,
                SessionId = sessionId,
                SuggestionCount = suggestionCount,
                SuggestionsApplied = suggestionsApplied,
                OutcomesWorked = worked,
                OutcomesFailed = failed,
                CorrectionCount = corrections,
                RepeatExplanationCount = reExplanations,
                TotalTokensIn = tokensIn,
                TotalTokensOut = tokensOut,
                SessionDurationMinutes = durationMin,
                EffortSavedMinutes = effortSaved
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<ExperimentResultSummary>> GetResultsAsync()
    {
        // Complex aggregation — use raw SQL for clarity
        return await _db.Database
            .SqlQueryRaw<ExperimentResultSummary>(
                """
                SELECT ea.Tier, ea.Format,
                    COUNT(DISTINCT ea.SessionId) AS Sessions,
                    AVG(CAST(em.OutcomesWorked AS FLOAT) / NULLIF(em.OutcomesWorked + em.OutcomesFailed, 0)) AS MeanSuccessRate,
                    AVG(CAST(em.CorrectionCount AS FLOAT)) AS MeanCorrectionCount,
                    AVG(em.ApiCostCents) AS AvgCostCents,
                    AVG(CAST(em.EffortSavedMinutes AS FLOAT)) AS AvgEffortSaved,
                    AVG(CAST(ISNULL(ea.TokensUsed, 0) AS FLOAT)) AS AvgTokensUsed,
                    CASE WHEN AVG(CAST(ISNULL(ea.TokensUsed, 0) AS FLOAT)) > 0
                         THEN AVG(CAST(em.EffortSavedMinutes AS FLOAT)) / (AVG(CAST(ISNULL(ea.TokensUsed, 0) AS FLOAT)) / 1000.0)
                         ELSE 0 END AS MinutesSavedPer1KTokens
                FROM ExperimentAssignments ea
                JOIN ExperimentMetrics em ON ea.AssignmentId = em.AssignmentId
                JOIN Experiments e ON ea.ExperimentId = e.ExperimentId
                WHERE e.Status = 'Active'
                GROUP BY ea.Tier, ea.Format
                ORDER BY ea.Tier, ea.Format
                """)
            .ToListAsync();
    }

    public async Task<List<ExperimentVerdict>> GetVerdictsAsync()
    {
        return await _db.Database
            .SqlQueryRaw<ExperimentVerdict>(
                """
                WITH TierStats AS (
                    SELECT ea.Tier, ea.Format,
                        COUNT(*) AS N,
                        AVG(CAST(em.OutcomesWorked AS FLOAT) / NULLIF(em.OutcomesWorked + em.OutcomesFailed, 0)) AS MeanRate
                    FROM ExperimentAssignments ea
                    JOIN ExperimentMetrics em ON ea.AssignmentId = em.AssignmentId
                    JOIN Experiments e ON ea.ExperimentId = e.ExperimentId
                    WHERE e.Status = 'Active'
                    GROUP BY ea.Tier, ea.Format
                )
                SELECT a.Tier, a.MeanRate AS AclMean, p.MeanRate AS ProseMean,
                    a.MeanRate - p.MeanRate AS Difference,
                    a.N AS AclSessions, p.N AS ProseSessions,
                    CASE
                        WHEN a.N < 30 OR p.N < 30 THEN 'INSUFFICIENT DATA'
                        WHEN ABS(a.MeanRate - p.MeanRate) < 0.05 THEN 'NO SIGNIFICANT DIFFERENCE'
                        WHEN a.MeanRate > p.MeanRate THEN 'ACL WINS'
                        ELSE 'PROSE WINS'
                    END AS Verdict
                FROM TierStats a
                JOIN TierStats p ON a.Tier = p.Tier
                WHERE a.Format = 'ACL' AND p.Format = 'Prose'
                """)
            .ToListAsync();
    }

    public async Task ConcludeAsync(int experimentId, ContextFormat winner, string conclusion)
    {
        await _db.Experiments
            .Where(e => e.ExperimentId == experimentId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(e => e.Status, ExperimentStatus.Concluded)
                .SetProperty(e => e.ConcludedAt, DateTime.Now)
                .SetProperty(e => e.WinnerFormat, winner)
                .SetProperty(e => e.Conclusion, conclusion));
    }

    public async Task PauseAsync(int experimentId)
    {
        await _db.Experiments
            .Where(e => e.ExperimentId == experimentId)
            .ExecuteUpdateAsync(x => x.SetProperty(e => e.Status, ExperimentStatus.Paused));
    }

    public async Task ResumeAsync(int experimentId)
    {
        await _db.Experiments
            .Where(e => e.ExperimentId == experimentId)
            .ExecuteUpdateAsync(x => x.SetProperty(e => e.Status, ExperimentStatus.Active));
    }
}
