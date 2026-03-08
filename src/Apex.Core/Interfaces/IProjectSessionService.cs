using Apex.Core.Models;

namespace Apex.Core.Interfaces;

public interface IProjectSessionService
{
    Task<(int SessionId, bool IsNew)> GetOrCreateAsync(string project);
    Task<string> ResolveProjectAsync(string? hint);
    Task<int?> GetActiveSessionIdAsync(string project);
    Task<List<Project>> GetAllProjectsAsync();
    Task CloseSessionAsync(string project);
}
