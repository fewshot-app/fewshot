using Apex.Core.Interfaces;
using Apex.Core.Models;
using Apex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Apex.Infrastructure.Services;

public class ProjectSessionService : IProjectSessionService
{
    private readonly ApexDbContext _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ProjectSessionService> _logger;

    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(36);

    public ProjectSessionService(ApexDbContext db, IConnectionMultiplexer redis, ILogger<ProjectSessionService> logger)
    {
        _db = db;
        _redis = redis;
        _logger = logger;
    }

    /// <summary>
    /// Returns today's session for the given project, creating one if needed.
    /// Key: apex:project-session:{project}:{yyyy-MM-dd}
    /// </summary>
    public async Task<(int SessionId, bool IsNew)> GetOrCreateAsync(string project)
    {
        var db = _redis.GetDatabase();
        var key = BuildKey(project);

        var cached = await db.StringGetAsync(key);
        if (cached.HasValue && int.TryParse(cached, out var existingId))
        {
            await db.KeyExpireAsync(key, SessionTtl); // touch TTL
            return (existingId, false);
        }

        // Create new SQL session
        var session = new Session
        {
            Project = project,
            StartTime = DateTime.UtcNow
        };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        await db.StringSetAsync(key, session.SessionId.ToString(), SessionTtl);
        _logger.LogInformation("Created new APEX session {SessionId} for project '{Project}'", session.SessionId, project);

        return (session.SessionId, true);
    }

    /// <summary>
    /// Resolves a free-text hint (e.g. "wordpress", "wvu divi", "apex middleware")
    /// to a project name by fuzzy-matching against Keywords from the DB.
    /// Falls back to "general" if no match found.
    /// </summary>
    public async Task<string> ResolveProjectAsync(string? hint)
    {
        if (string.IsNullOrWhiteSpace(hint))
            return "general";

        var projects = await _db.Projects
            .Where(p => p.IsActive)
            .ToListAsync();

        var hintLower = hint.ToLowerInvariant();

        // Score each project by how many of its keywords appear in the hint
        var best = projects
            .Select(p => new
            {
                p.Name,
                Score = p.KeywordList.Count(kw => hintLower.Contains(kw))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        var resolved = best?.Name ?? "general";
        _logger.LogDebug("Resolved hint '{Hint}' → project '{Project}'", hint, resolved);
        return resolved;
    }

    /// <summary>
    /// Gets the current active session ID for a project from Redis (null if none).
    /// </summary>
    public async Task<int?> GetActiveSessionIdAsync(string project)
    {
        var db = _redis.GetDatabase();
        var cached = await db.StringGetAsync(BuildKey(project));
        if (cached.HasValue && int.TryParse(cached, out var id))
            return id;
        return null;
    }

    /// <summary>
    /// Explicitly closes today's session for a project and removes it from Redis.
    /// The Hangfire consolidation job will pick it up overnight.
    /// </summary>
    public async Task CloseSessionAsync(string project)
    {
        var db = _redis.GetDatabase();
        var key = BuildKey(project);
        var cached = await db.StringGetAsync(key);

        if (cached.HasValue && int.TryParse(cached, out var sessionId))
        {
            await _db.Sessions
                .Where(s => s.SessionId == sessionId)
                .ExecuteUpdateAsync(x => x.SetProperty(s => s.EndTime, DateTime.UtcNow));

            await db.KeyDeleteAsync(key);
            _logger.LogInformation("Closed APEX session {SessionId} for project '{Project}'", sessionId, project);
        }
    }

    /// <summary>
    /// Returns all active projects from the DB.
    /// </summary>
    public async Task<List<Project>> GetAllProjectsAsync() =>
        await _db.Projects.Where(p => p.IsActive).OrderBy(p => p.DisplayName).ToListAsync();

    private static string BuildKey(string project) =>
        $"apex:project-session:{project.ToLowerInvariant()}:{DateTime.UtcNow:yyyy-MM-dd}";
}
