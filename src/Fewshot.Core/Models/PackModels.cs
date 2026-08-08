namespace Fewshot.Core.Models;

/// <summary>
/// Top-level structure of a .fewshotpack file (JSON before encryption).
/// Contains curated memories, preferences, and anti-patterns for a domain.
/// </summary>
public class FewshotPack
{
    public string PackId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public string TargetProject { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<PackMemory> Memories { get; set; } = [];
    public List<PackPreference> Preferences { get; set; } = [];
    public List<PackAntiPattern> AntiPatterns { get; set; } = [];
}

public class PackMemory
{
    public string Summary { get; set; } = string.Empty;
    public string? Solution { get; set; }
    public string? Approach { get; set; }
    public string? OutcomeLabel { get; set; }
    public string? Tags { get; set; }
    public string? Language { get; set; }
}

public class PackPreference
{
    public string Category { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; } = 0.7;
}

public class PackAntiPattern
{
    public string Pattern { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? ErrorCode { get; set; }
}

/// <summary>
/// Encrypted envelope wrapping an FewshotPack for distribution.
/// </summary>
public class EncryptedPackEnvelope
{
    public string Format { get; set; } = "fewshotpack-v2";
    public string PackId { get; set; } = string.Empty;
    public string Cipher { get; set; } = "AES-256-GCM";
    public string Iv { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

/// <summary>
/// Cached license activation record — avoids re-calling the license server
/// on repeat imports of the same pack.
/// </summary>
public class LicenseActivationCache
{
    public int Id { get; set; }
    public string LicenseKey { get; set; } = string.Empty;
    public string PackId { get; set; } = string.Empty;
    public string MachineId { get; set; } = string.Empty;
    public string DecryptionKey { get; set; } = string.Empty;
    public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Result of a pack import operation.
/// </summary>
public class PackImportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string PackId { get; set; } = string.Empty;
    public string PackName { get; set; } = string.Empty;
    public int MemoriesImported { get; set; }
    public int PreferencesImported { get; set; }
    public int AntiPatternsImported { get; set; }
    public int DuplicatesSkipped { get; set; }
}
