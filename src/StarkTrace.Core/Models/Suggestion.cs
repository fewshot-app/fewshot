using StarkTrace.Core.Enums;

namespace StarkTrace.Core.Models;

public class Suggestion
{
    public int SuggestionId { get; set; }
    public int MessageId { get; set; }
    public SuggestionType SuggestionType { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? FilePath { get; set; }
    public ExtractionMethod ExtractionMethod { get; set; }
    public double? ExtractionConfidence { get; set; }
    public bool IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
}
