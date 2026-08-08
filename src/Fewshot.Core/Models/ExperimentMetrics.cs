namespace Fewshot.Core.Models;

public class ExperimentMetrics
{
    public int MetricId { get; set; }
    public int AssignmentId { get; set; }
    public int SessionId { get; set; }

    // Response quality
    public int SuggestionCount { get; set; }
    public int SuggestionsApplied { get; set; }
    public int OutcomesWorked { get; set; }
    public int OutcomesFailed { get; set; }

    // Negative signals
    public int CorrectionCount { get; set; }
    public int RepeatExplanationCount { get; set; }

    // Efficiency
    public int? TotalTokensIn { get; set; }
    public int? TotalTokensOut { get; set; }
    public double? SessionDurationMinutes { get; set; }
    public int? MessagesToFirstUseful { get; set; }

    // Cost
    public double? ApiCostCents { get; set; }
    public int? EffortSavedMinutes { get; set; }

    public DateTime ComputedAt { get; set; }
}
