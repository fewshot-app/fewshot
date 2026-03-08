namespace Apex.Core.Interfaces;

/// <summary>
/// Determines whether APEX has accumulated enough data to enable autonomous actions.
/// Hard gate prevents premature agency before the system has learned developer patterns.
/// </summary>
public interface IAgencyGate
{
    /// <summary>
    /// Check if all agency prerequisites are met.
    /// </summary>
    Task<AgencyReadiness> CheckReadinessAsync();
}

/// <summary>
/// Detailed readiness status for the agency gate.
/// </summary>
public class AgencyReadiness
{
    public bool IsReady { get; set; }
    public int SuggestionsLogged { get; set; }
    public int SuggestionsRequired { get; set; }
    public double ExplicitFeedbackRate { get; set; }
    public double FeedbackRateRequired { get; set; }
    public int AntiPatternSuppressions { get; set; }
    public int SuppressionsRequired { get; set; }
    public int ConsolidatedSessions { get; set; }
    public int ConsolidatedSessionsRequired { get; set; }
    public List<string> BlockingReasons { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// Configurable agency gate thresholds. Users can override via appsettings.
/// </summary>
public class AgencyGateOptions
{
    public const string SectionName = "Apex:AgencyGate";

    // User-configured thresholds (can be lowered)
    public int MinSuggestions { get; set; } = 30;
    public double MinFeedbackRate { get; set; } = 0.40;
    public int MinAntiPatternSuppressions { get; set; } = 1;
    public int MinConsolidatedSessions { get; set; } = 5;

    // Recommended defaults (used for warnings)
    public static int RecommendedSuggestions => 30;
    public static double RecommendedFeedbackRate => 0.40;
    public static int RecommendedSuppressions => 1;
    public static int RecommendedConsolidatedSessions => 5;
}
