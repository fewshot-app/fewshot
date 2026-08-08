using Fewshot.Core.Enums;

namespace Fewshot.Core.Models;

/// <summary>
/// Format assignment plan for a single session across all tiers.
/// </summary>
public class SessionFormatPlan
{
    public int SessionId { get; set; }
    public Dictionary<ContextTier, ContextFormat> TierFormats { get; set; } = [];
}

/// <summary>
/// Aggregated experiment results for dashboard display.
/// </summary>
public class ExperimentResultSummary
{
    public ContextTier Tier { get; set; }
    public ContextFormat Format { get; set; }
    public int Sessions { get; set; }
    public double MeanSuccessRate { get; set; }
    public double MeanCorrectionCount { get; set; }
    public double AvgCostCents { get; set; }
    public double AvgEffortSaved { get; set; }
    public double AvgTokensUsed { get; set; }
    public double MinutesSavedPer1KTokens { get; set; }
}

/// <summary>
/// Per-tier verdict from the decision query.
/// </summary>
/// <summary>
/// Token efficiency comparison per tier/format — self-resolves from assignment data.
/// </summary>
public class ExperimentTokenSummary
{
    public ContextTier Tier { get; set; }
    public ContextFormat Format { get; set; }
    public int Sessions { get; set; }
    public double AvgTokensUsed { get; set; }
    public int MinTokensUsed { get; set; }
    public int MaxTokensUsed { get; set; }
    public int TokenBudget { get; set; }
    public double AvgUtilizationPct { get; set; }
}

public class ExperimentVerdict
{
    public ContextTier Tier { get; set; }
    public double AclMean { get; set; }
    public double ProseMean { get; set; }
    public double Difference { get; set; }
    public int AclSessions { get; set; }
    public int ProseSessions { get; set; }
    public string Verdict { get; set; } = string.Empty;
}
