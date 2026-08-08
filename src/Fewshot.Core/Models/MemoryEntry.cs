namespace Fewshot.Core.Models;

/// <summary>
/// Semantic memory record. Embedding stored as raw float bytes (BLOB in SQLite).
/// </summary>
public class MemoryEntry
{
    public string PointId { get; set; } = string.Empty;
    public int SessionId { get; set; }
    public string? Project { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? Solution { get; set; }
    public string? Approach { get; set; }
    public string? OutcomeLabel { get; set; }
    public string? Tags { get; set; }
    public string? Language { get; set; }
    public byte[] Embedding { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
