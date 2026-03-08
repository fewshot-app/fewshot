namespace Apex.Core.Models;

public class AntiPattern
{
    public int AntiPatternId { get; set; }
    public int SessionId { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime CreatedAt { get; set; }
}
