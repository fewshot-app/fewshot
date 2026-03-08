using Apex.Core.Interfaces;
using Apex.Core.Models;
using Apex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Apex.Infrastructure.Services;

public class ProjectSessionService : IProjectSessionService
{
    private readonly ApexDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProjectSessionService> _logger;

    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(36);

    public ProjectSessionService(ApexDbContext db, IMemoryCache cache, ILogger<ProjectSessionService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<(int SessionId, bool IsNew)> GetOrCreateAsync(string project)
    {
        var key = BuildKey(project);

        if (_cache.TryGetValue(key, out int existingId))
            return (existingId, false);

        var session = new Session { Project = project, StartTime = DateTime.UtcNow };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        _cache.Set(key, session.SessionId, SessionTtl);
        _logger.LogInformation("Created new APEX session {SessionId} for project '{Project}'", session.SessionId, project);

        return (session.SessionId, true);
    }

    public async Task<string> ResolveProjectAsync(string? hint)
    {
        if (string.IsNullOrWhiteSpace(hint))
            return "general";

        var projects = await _db.Projects.Where(p => p.IsActive).ToListAsync();
        var hintLower = hint.ToLowerInvariant();

        var best = projects
            .Select(p => new { p.Name, Score = p.KeywordList.Count(kw => hintLower.Contains(kw)) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        var resolved = best?.Name ?? "general";
        _logger.LogDebug("Resolved hint '{Hint}' → project '{Project}'", hint, resolved);
        return resolved;
    }

    public Task<int?> GetActiveSessionIdAsync(string project)
    {
        var key = BuildKey(project);
        var result = _cache.TryGetValue(key, out int id) ? (int?)id : null;
        return Task.FromResult(result);
    }

    public async Task CloseSessionAsync(string project)
    {
        var key = BuildKey(project);

        if (_cache.TryGetValue(key, out int sessionId))
        {
            await _db.Sessions
                .Where(s => s.SessionId == sessionId)
                .ExecuteUpdateAsync(x => x.SetProperty(s => s.EndTime, DateTime.UtcNow));

            _cache.Remove(key);
            _logger.LogInformation("Closed APEX session {SessionId} for project '{Project}'", sessionId, project);
        }
    }

    public async Task<List<Project>> GetAllProjectsAsync() =>
        await _db.Projects.Where(p => p.IsActive).OrderBy(p => p.DisplayName).ToListAsync();

    private static string BuildKey(string project) =>
        $"apex:session:{project.ToLowerInvariant()}:{DateTime.UtcNow:yyyy-MM-dd}";
}
