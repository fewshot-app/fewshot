namespace Fewshot.Core.Interfaces;

/// <summary>
/// Provides user-configured allowlist regex patterns that suppress audit findings
/// in matched content regions. Backed by SystemSettings (key: Audit:Allowlist).
/// </summary>
public interface IAuditAllowlistProvider
{
    /// <summary>Current pattern strings (raw regex source, not compiled).</summary>
    Task<IReadOnlyList<string>> GetPatternsAsync();

    /// <summary>
    /// Character ranges of <paramref name="content"/> matched by any allowlist pattern.
    /// Ranges are merged; End is exclusive.
    /// </summary>
    Task<IReadOnlyList<(int Start, int End)>> GetMatchRangesAsync(string content);

    /// <summary>Replaces the full pattern list. Throws ArgumentException on invalid regex.</summary>
    Task SetPatternsAsync(IEnumerable<string> patterns);

    /// <summary>Drops the cached compiled patterns; next call re-reads from the database.</summary>
    void InvalidateCache();
}
