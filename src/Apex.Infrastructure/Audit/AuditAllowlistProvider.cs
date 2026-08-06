using System.Text.Json;
using System.Text.RegularExpressions;
using Apex.Core.Interfaces;
using Apex.Core.Models;
using Apex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Apex.Infrastructure.Audit;

/// <summary>
/// Singleton provider for the audit allowlist. Patterns are stored as a JSON string
/// array in SystemSettings (key: Audit:Allowlist) and cached compiled for 60 seconds.
/// </summary>
public class AuditAllowlistProvider : IAuditAllowlistProvider
{
    public const string SettingKey = "Audit:Allowlist";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditAllowlistProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private List<Regex> _compiled = [];
    private List<string> _patterns = [];
    private DateTime _loadedAt = DateTime.MinValue;

    public AuditAllowlistProvider(IServiceScopeFactory scopeFactory, ILogger<AuditAllowlistProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> GetPatternsAsync()
    {
        await EnsureFreshAsync();
        return _patterns;
    }

    public async Task<IReadOnlyList<(int Start, int End)>> GetMatchRangesAsync(string content)
    {
        await EnsureFreshAsync();
        if (_compiled.Count == 0 || string.IsNullOrEmpty(content))
            return [];

        var ranges = new List<(int Start, int End)>();
        foreach (var regex in _compiled)
        {
            try
            {
                foreach (Match m in regex.Matches(content))
                    if (m.Length > 0)
                        ranges.Add((m.Index, m.Index + m.Length));
            }
            catch (RegexMatchTimeoutException)
            {
                _logger.LogWarning("Allowlist pattern timed out and was skipped: {Pattern}", regex);
            }
        }

        return MergeRanges(ranges);
    }

    public async Task SetPatternsAsync(IEnumerable<string> patterns)
    {
        var list = patterns.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct().ToList();

        // Validate every regex compiles before persisting anything
        foreach (var p in list)
        {
            try { _ = new Regex(p, RegexOptions.None, MatchTimeout); }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Invalid regex pattern '{p}': {ex.Message}", ex);
            }
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApexDbContext>();

        var json = JsonSerializer.Serialize(list);
        var existing = await db.SystemSettings.FindAsync(SettingKey);
        if (existing is null)
            db.SystemSettings.Add(new SystemSetting { Key = SettingKey, Value = json, UpdatedAt = DateTime.UtcNow });
        else
        {
            existing.Value = json;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();

        InvalidateCache();
    }

    public void InvalidateCache() => _loadedAt = DateTime.MinValue;

    private async Task EnsureFreshAsync()
    {
        if (DateTime.UtcNow - _loadedAt < CacheTtl) return;

        await _lock.WaitAsync();
        try
        {
            if (DateTime.UtcNow - _loadedAt < CacheTtl) return; // double-check under lock

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApexDbContext>();
            var setting = await db.SystemSettings.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == SettingKey);

            var patterns = new List<string>();
            if (setting is not null && !string.IsNullOrWhiteSpace(setting.Value))
            {
                try { patterns = JsonSerializer.Deserialize<List<string>>(setting.Value) ?? []; }
                catch (JsonException ex)
                {
                    _logger.LogWarning("Audit:Allowlist setting is not valid JSON — treating as empty: {Error}", ex.Message);
                }
            }

            var compiled = new List<Regex>();
            foreach (var p in patterns)
            {
                try { compiled.Add(new Regex(p, RegexOptions.Compiled, MatchTimeout)); }
                catch (ArgumentException ex)
                {
                    _logger.LogWarning("Skipping invalid allowlist pattern '{Pattern}': {Error}", p, ex.Message);
                }
            }

            _patterns = patterns;
            _compiled = compiled;
            _loadedAt = DateTime.UtcNow;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static List<(int Start, int End)> MergeRanges(List<(int Start, int End)> ranges)
    {
        if (ranges.Count <= 1) return ranges;

        ranges.Sort((a, b) => a.Start.CompareTo(b.Start));
        var merged = new List<(int Start, int End)> { ranges[0] };
        for (var i = 1; i < ranges.Count; i++)
        {
            var last = merged[^1];
            if (ranges[i].Start <= last.End)
                merged[^1] = (last.Start, Math.Max(last.End, ranges[i].End));
            else
                merged.Add(ranges[i]);
        }
        return merged;
    }
}
