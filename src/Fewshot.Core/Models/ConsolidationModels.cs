namespace Fewshot.Core.Models;

/// <summary>
/// Structured extraction result from Ollama consolidation.
/// </summary>
public class ConsolidationExtraction
{
    /// <summary>
    /// Memories worth preserving in Qdrant for future sessions.
    /// </summary>
    public List<ExtractedMemory> Memories { get; set; } = [];

    /// <summary>
    /// Patterns that failed or should be avoided.
    /// </summary>
    public List<ExtractedAntiPattern> AntiPatterns { get; set; } = [];

    /// <summary>
    /// Inferred developer preferences from observed behavior.
    /// </summary>
    public List<ExtractedPreference> Preferences { get; set; } = [];

    /// <summary>
    /// Actionable suggestions made by the assistant during the session.
    /// </summary>
    public List<ExtractedSuggestion> Suggestions { get; set; } = [];

    /// <summary>
    /// One-line summary of the session.
    /// </summary>
    public string SessionSummary { get; set; } = string.Empty;
}

public class ExtractedMemory
{
    public string Summary { get; set; } = string.Empty;
    public string? Solution { get; set; }
    public string? OutcomeLabel { get; set; }
    public string? Tags { get; set; }
    public string? Language { get; set; }
    public string? Project { get; set; }
}

public class ExtractedAntiPattern
{
    public string Pattern { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? ErrorCode { get; set; }
}

public class ExtractedPreference
{
    public string Category { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class ExtractedSuggestion
{
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // CodeSnippet, ArchitecturalPattern, ConfigChange
    public string? Language { get; set; }
    public string? FilePath { get; set; }
    public double Confidence { get; set; } = 0.5;
}

/// <summary>
/// Quality gate check result for session consolidation.
/// </summary>
public class ConsolidationQualityResult
{
    public bool ShouldConsolidate { get; set; }
    public string? SkipReason { get; set; }
    public int MessageCount { get; set; }
    public int TotalChars { get; set; }
    public int CorrectionCount { get; set; }
}
