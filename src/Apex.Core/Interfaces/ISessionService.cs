using Apex.Core.Models;

namespace Apex.Core.Interfaces;

public interface ISessionService
{
    Task<Session> StartSessionAsync();
    Task EndSessionAsync(int sessionId);

    Task<int> CloseStaleActiveSessionsAsync(TimeSpan idleThreshold);
    Task<Session?> GetSessionAsync(int sessionId);
    Task<Session?> GetActiveSessionAsync();
    Task<List<Session>> GetAllSessionsAsync();
    Task<List<Session>> GetUnconsolidatedSessionsAsync();
    Task MarkConsolidatedAsync(int sessionId, string summary);
    Task MarkConsolidationFailedAsync(int sessionId, string error);
    Task UpdateSessionAsync(Session session);
}
