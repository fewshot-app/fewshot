namespace Fewshot.Core.Models;

public class Preference
{
    public int PrefId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; } = 0.5;
    public int ReinforcementCount { get; set; }
    public bool IsExplicit { get; set; }
    public int? SourceSessionId { get; set; }
    public DateTime LastUpdated { get; set; }
}
