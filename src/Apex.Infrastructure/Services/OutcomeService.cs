using Apex.Core.Interfaces;
using Apex.Core.Models;
using Apex.Core.Enums;
using Apex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Apex.Infrastructure.Services;

public class OutcomeService : IOutcomeService
{
    private readonly ApexDbContext _db;

    public OutcomeService(ApexDbContext db) => _db = db;

    public async Task<Outcome> RecordAsync(Outcome o)
    {
        _db.Outcomes.Add(o);
        await _db.SaveChangesAsync();
        return o;
    }

    public async Task<List<Outcome>> GetBySuggestionAsync(int suggestionId)
    {
        return await _db.Outcomes
            .Where(o => o.SuggestionId == suggestionId)
            .OrderBy(o => o.FeedbackAt)
            .ToListAsync();
    }

    public async Task<(int Worked, int Failed)> GetCountsBySessionAsync(int sessionId)
    {
        var sessionMessageIds = _db.Messages
            .Where(m => m.SessionId == sessionId)
            .Select(m => m.MessageId);

        var sessionSuggestionIds = _db.Suggestions
            .Where(s => sessionMessageIds.Contains(s.MessageId))
            .Select(s => s.SuggestionId);

        var outcomes = await _db.Outcomes
            .Where(o => sessionSuggestionIds.Contains(o.SuggestionId))
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Worked = g.Count(o => o.Status == OutcomeStatus.Worked),
                Failed = g.Count(o => o.Status == OutcomeStatus.Failed)
            })
            .FirstOrDefaultAsync();

        return (outcomes?.Worked ?? 0, outcomes?.Failed ?? 0);
    }

    public async Task<int> GetEffortSavedBySessionAsync(int sessionId)
    {
        var sessionMessageIds = _db.Messages
            .Where(m => m.SessionId == sessionId)
            .Select(m => m.MessageId);

        var sessionSuggestionIds = _db.Suggestions
            .Where(s => sessionMessageIds.Contains(s.MessageId))
            .Select(s => s.SuggestionId);

        return await _db.Outcomes
            .Where(o => sessionSuggestionIds.Contains(o.SuggestionId) && o.EffortSavedMinutes != null)
            .SumAsync(o => o.EffortSavedMinutes ?? 0);
    }
}
