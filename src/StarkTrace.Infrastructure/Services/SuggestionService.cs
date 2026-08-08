using StarkTrace.Core.Interfaces;
using StarkTrace.Core.Models;
using StarkTrace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace StarkTrace.Infrastructure.Services;

public class SuggestionService : ISuggestionService
{
    private readonly StarkTraceDbContext _db;

    public SuggestionService(StarkTraceDbContext db) => _db = db;

    public async Task<Suggestion> CreateAsync(Suggestion s)
    {
        _db.Suggestions.Add(s);
        await _db.SaveChangesAsync();
        return s;
    }

    public async Task<List<Suggestion>> GetBySessionAsync(int sessionId)
    {
        return await _db.Suggestions
            .Where(s => _db.Messages
                .Where(m => m.SessionId == sessionId)
                .Select(m => m.MessageId)
                .Contains(s.MessageId))
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task MarkAppliedAsync(int suggestionId)
    {
        await _db.Suggestions
            .Where(s => s.SuggestionId == suggestionId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(s => s.IsApplied, true)
                .SetProperty(s => s.AppliedAt, DateTime.Now));
    }

    public async Task<int> GetCountBySessionAsync(int sessionId)
    {
        return await _db.Suggestions
            .CountAsync(s => _db.Messages
                .Where(m => m.SessionId == sessionId)
                .Select(m => m.MessageId)
                .Contains(s.MessageId));
    }

    public async Task<int> GetAppliedCountBySessionAsync(int sessionId)
    {
        return await _db.Suggestions
            .CountAsync(s => s.IsApplied && _db.Messages
                .Where(m => m.SessionId == sessionId)
                .Select(m => m.MessageId)
                .Contains(s.MessageId));
    }
}
