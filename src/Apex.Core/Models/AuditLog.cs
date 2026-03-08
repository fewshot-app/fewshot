namespace Apex.Core.Models;

public class AuditLog
{
    public int AuditId { get; set; }
    public int SessionId { get; set; }
    public string DetectedType { get; set; } = string.Empty;
    public string? FilePathHash { get; set; }
    public int FindingCount { get; set; }
    public bool WasBlocked { get; set; }
    public bool WasRedacted { get; set; }
    public DateTime AuditedAt { get; set; }
}
