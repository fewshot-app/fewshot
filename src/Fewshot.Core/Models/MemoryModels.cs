namespace Fewshot.Core.Models;

/// <summary>
/// Request to store a new semantic memory.
/// </summary>
public class MemoryStoreRequest
{
    public int SessionId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? Solution { get; set; }
    public string? Approach { get; set; }
    public string? OutcomeLabel { get; set; }
    public string? Tags { get; set; }
    public string? Language { get; set; }
    public string? Project { get; set; }
}

/// <summary>
/// A memory stored in Qdrant with its metadata and embedding info.
/// </summary>
public class StoredMemory
{
    public string PointId { get; set; } = string.Empty;
    public int SessionId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? Solution { get; set; }
    public string? Approach { get; set; }
    public string? OutcomeLabel { get; set; }
    public string? Tags { get; set; }
    public string? Language { get; set; }
    public string? Project { get; set; }
    public DateTime CreatedAt { get; set; }
    public double? RelevanceScore { get; set; }
}

/// <summary>
/// Quality gate result for memory promotion.
/// </summary>
public class MemoryQualityResult
{
    public bool Passed { get; set; }
    public string? RejectionReason { get; set; }
    public bool IsDuplicate { get; set; }
    public double? DuplicateScore { get; set; }
}
