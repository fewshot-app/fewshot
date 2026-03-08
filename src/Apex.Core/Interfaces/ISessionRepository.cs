using Apex.Core.Models;

namespace Apex.Core.Interfaces;

public interface ISessionRepository
{
    Task<Session> CreateAsync();
    Task<Session?> GetAsync(int sessionId);
    Task EndAsync(int sessionId);
    Task<List<Session>> GetUnconsolidatedAsync();
    Task MarkConsolidatedAsync(int sessionId, string summary);
    Task MarkConsolidationFailedAsync(int sessionId, string error);
    Task UpdateContextHashAsync(int sessionId, string hash);
}
