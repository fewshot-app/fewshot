using Apex.Core.Interfaces;
using Apex.Core.Models;
using Apex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Apex.Infrastructure.Services;

public class SessionService : ISessionService
{
    private readonly ApexDbContext _db;

    public SessionService(ApexDbContext db) => _db = db;

    public async Task<Session> StartSessionAsync()
    {
        var session = new Session { StartTime = DateTime.UtcNow };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    public async Task EndSessionAsync(int sessionId)
    {
        await _db.Sessions
            .Where(s => s.SessionId == sessionId)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.EndTime, DateTime.UtcNow));
    }

    public async Task<int> CloseStaleActiveSessionsAsync(TimeSpan idleThreshold)
    {
        var cutoff = DateTime.UtcNow - idleThreshold;

        var candidates = await _db.Sessions
            .Where(s => s.EndTime == null)
            .Select(s => new
            {
                s.SessionId,
                s.StartTime,
                LastMessage = _db.Messages
                    .Where(m => m.SessionId == s.SessionId)
                    .Max(m => (DateTime?)m.Timestamp)
            })
            .ToListAsync();

        var staleIds = candidates
            .Where(c => (c.LastMessage ?? c.StartTime) <= cutoff)
            .Select(c => c.SessionId)
            .ToList();

        if (staleIds.Count == 0) return 0;

        await _db.Sessions
            .Where(s => staleIds.Contains(s.SessionId))
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.EndTime, DateTime.UtcNow));

        return staleIds.Count;
    }

    public async Task<Session?> GetSessionAsync(int sessionId)
    {
        return await _db.Sessions.FindAsync(sessionId);
    }

    public async Task<List<Session>> GetAllSessionsAsync()
    {
        return await _db.Sessions.OrderByDescending(s => s.StartTime).ToListAsync();
    }

    public async Task<Session?> GetActiveSessionAsync()
    {
        return await _db.Sessions
            .Where(s => s.EndTime == null)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Session>> GetUnconsolidatedSessionsAsync()
    {
        return await _db.Sessions
            .Where(s => s.EndTime != null && !s.IsConsolidated)
            .OrderBy(s => s.StartTime)
            .ToListAsync();
    }

    public async Task MarkConsolidatedAsync(int sessionId, string summary)
    {
        await _db.Sessions
            .Where(s => s.SessionId == sessionId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(s => s.IsConsolidated, true)
                .SetProperty(s => s.ConsolidatedAt, DateTime.UtcNow)
                .SetProperty(s => s.ConsolidationSummary, summary)
                .SetProperty(s => s.ConsolidationError, (string?)null));
    }

    public async Task MarkConsolidationFailedAsync(int sessionId, string error)
    {
        await _db.Sessions
            .Where(s => s.SessionId == sessionId)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.ConsolidationError, error));
    }

    public async Task UpdateSessionAsync(Session session)
    {
        _db.Sessions.Update(session);
        await _db.SaveChangesAsync();
    }
}
