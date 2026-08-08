namespace StarkTrace.Core.Models;

public class Session
{
    public int SessionId { get; set; }
    public string? Project { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? ContextHash { get; set; }
    public bool IsConsolidated { get; set; }
    public DateTime? ConsolidatedAt { get; set; }
    public string? ConsolidationSummary { get; set; }
    public string? ConsolidationError { get; set; }
}
