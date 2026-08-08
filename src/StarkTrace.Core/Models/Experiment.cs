using StarkTrace.Core.Enums;

namespace StarkTrace.Core.Models;

public class Experiment
{
    public int ExperimentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ContextTier Tier { get; set; }
    public ExperimentStatus Status { get; set; } = ExperimentStatus.Active;
    public int TargetSessions { get; set; } = 60;
    public DateTime StartedAt { get; set; }
    public DateTime? ConcludedAt { get; set; }
    public ContextFormat? WinnerFormat { get; set; }
    public string? Conclusion { get; set; }
}
