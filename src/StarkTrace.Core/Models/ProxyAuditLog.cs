namespace StarkTrace.Core.Models;

public class ProxyAuditLog
{
    public int Id { get; set; }
    public string Direction { get; set; } = string.Empty;   // "outbound" | "inbound"
    public string Method { get; set; } = string.Empty;      // MCP method name e.g. "tools/call"
    public string FindingTypes { get; set; } = string.Empty; // comma-separated
    public int FindingCount { get; set; }
    public double MaxConfidence { get; set; }
    public bool WasRedacted { get; set; }
    public string? Snippet { get; set; }
    public string Source { get; set; } = "starktrace-proxy";
    public DateTime Timestamp { get; set; }
}
