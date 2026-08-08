using StarkTrace.Core.Enums;

namespace StarkTrace.Core.Models;

/// <summary>
/// Runtime result from the three-stage audit pipeline.
/// Not persisted directly — feeds into AuditLog table.
/// </summary>
public class AuditPipelineResult
{
    public bool IsSafe { get; set; }
    public bool RequiresReview { get; set; }
    public AuditVerdict Verdict => (IsSafe, RequiresReview) switch
    {
        (false, _) => AuditVerdict.Blocked,
        (true, true) => AuditVerdict.SafeWithRedaction,
        _ => AuditVerdict.Safe
    };
    public string? RedactedContent { get; set; }
    public List<AuditFinding> Findings { get; set; } = [];
}

public class AuditFinding
{
    public string DetectedType { get; set; } = string.Empty;
    public AuditStage Stage { get; set; }
    public double Confidence { get; set; }
    public int StartIndex { get; set; }
    public int Length { get; set; }
}

public enum AuditStage
{
    Regex,
    Presidio,
    Entropy
}
