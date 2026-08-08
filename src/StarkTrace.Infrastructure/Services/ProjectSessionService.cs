using StarkTrace.Core.Interfaces;
using StarkTrace.Core.Models;
using StarkTrace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace StarkTrace.Infrastructure.Services;

public class ProjectSessionService : IProjectSessionService
{
    private readonly StarkTraceDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProjectSessionService> _logger;

    private static readonly TimeSpan IdleWindow = TimeSpan.FromHours(12);

    public ProjectSessionService(StarkTraceDbContext db, IMemoryCache cache, ILogger<ProjectSessionService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<(int SessionId, bool IsNew)> GetOrCreateAsync(string project)
    {
        var key = BuildKey(project);

        if (_cache.TryGetValue(key, out int cachedId))
            return (cachedId, false);

        var active = await _db.Sessions
            .AsNoTracking()
            .Where(s => s.Project == project && s.EndTime == null)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync();

        if (active is not null)
        {
            var lastActivity = await _db.Messages
                .Where(m => m.SessionId == active.SessionId)
                .MaxAsync(m => (DateTime?)m.Timestamp) ?? active.StartTime;

            if (DateTime.UtcNow - lastActivity < IdleWindow)
            {
                _cache.Set(key, active.SessionId, IdleWindow);
                return (active.SessionId, false);
            }

            await _db.Sessions
                .Where(s => s.SessionId == active.SessionId)
                .ExecuteUpdateAsync(x => x.SetProperty(s => s.EndTime, DateTime.UtcNow));
            _logger.LogInformation("Closed stale session {SessionId} for project '{Project}'", active.SessionId, project);
        }

        var session = new Session { Project = project, StartTime = DateTime.UtcNow };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        _cache.Set(key, session.SessionId, IdleWindow);
        _logger.LogInformation("Created new StarkTrace session {SessionId} for project '{Project}'", session.SessionId, project);

        return (session.SessionId, true);
    }

    public async Task<string> ResolveProjectAsync(string? hint)
    {
        if (string.IsNullOrWhiteSpace(hint))
            return "general";

        var projects = await _db.Projects.Where(p => p.IsActive).ToListAsync();
        var hintLower = hint.ToLowerInvariant().Trim();

        var exactMatch = projects.FirstOrDefault(p => p.Name.Equals(hintLower, StringComparison.OrdinalIgnoreCase));
        if (exactMatch is not null)
        {
            _logger.LogDebug("Resolved hint '{Hint}' → project '{Project}' (exact name match)", hint, exactMatch.Name);
            return exactMatch.Name;
        }

        var best = projects
            .Select(p => new { p.Name, Score = p.KeywordList.Count(kw => hintLower.Contains(kw.ToLowerInvariant())) })
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
        _cache.Remove(BuildKey(project));

        var closed = await _db.Sessions
            .Where(s => s.Project == project && s.EndTime == null)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.EndTime, DateTime.UtcNow));

        if (closed > 0)
            _logger.LogInformation("Closed {Count} active session(s) for project '{Project}'", closed, project);
    }

    public async Task<List<Project>> GetAllProjectsAsync() =>
        await _db.Projects.Where(p => p.IsActive).OrderBy(p => p.DisplayName).ToListAsync();

    private static string BuildKey(string project) =>
        $"starktrace:session:{project.ToLowerInvariant()}";
}
