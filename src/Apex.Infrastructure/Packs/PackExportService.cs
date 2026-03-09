using Apex.Core.Models;
using Apex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Apex.Infrastructure.Packs;

/// <summary>
/// Exports a project's memories, preferences, and anti-patterns as an ApexPack.
/// </summary>
public class PackExportService
{
    private readonly ApexDbContext _db;
    private readonly ILogger<PackExportService> _log;

    public PackExportService(ApexDbContext db, ILogger<PackExportService> log)
    {
        _db = db;
        _log = log;
    }

    /// <summary>
    /// Export all knowledge for a project as an unencrypted ApexPack.
    /// </summary>
    public async Task<ApexPack?> ExportAsync(string projectName, string? author = null)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Name == projectName);
        if (project == null) return null;

        var memories = await _db.Memories
            .Where(m => m.Project == projectName)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        // Get all preferences (not project-scoped, but included in pack)
        var preferences = await _db.Preferences
            .Where(p => p.ConfidenceScore >= 0.5) // Only export reasonably confident prefs
            .OrderByDescending(p => p.ConfidenceScore)
            .ToListAsync();

        var antiPatterns = await _db.AntiPatterns
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var pack = new ApexPack
        {
            PackId = $"{projectName}-{DateTime.UtcNow:yyyyMMdd}",
            Name = $"{project.DisplayName} Pack",
            Description = $"Exported from APEX — {memories.Count} memories, {preferences.Count} preferences, {antiPatterns.Count} anti-patterns",
            Version = "1.0.0",
            Author = author ?? Environment.UserName,
            TargetProject = projectName,
            CreatedAt = DateTime.UtcNow,
            Memories = memories.Select(m => new PackMemory
            {
                Summary = m.Summary,
                Solution = m.Solution,
                Approach = m.Approach,
                OutcomeLabel = m.OutcomeLabel,
                Tags = m.Tags,
                Language = m.Language
            }).ToList(),
            Preferences = preferences.Select(p => new PackPreference
            {
                Category = p.Category,
                Key = p.Key,
                Value = p.Value,
                ConfidenceScore = p.ConfidenceScore
            }).ToList(),
            AntiPatterns = antiPatterns.Select(a => new PackAntiPattern
            {
                Pattern = a.Pattern,
                Reason = a.Reason,
                Language = a.Language,
                ErrorCode = a.ErrorCode
            }).ToList()
        };

        _log.LogInformation("Exported pack '{PackId}': {Mem} memories, {Pref} prefs, {Ap} anti-patterns",
            pack.PackId, pack.Memories.Count, pack.Preferences.Count, pack.AntiPatterns.Count);

        return pack;
    }
}
