using Fewshot.Core.Enums;

namespace Fewshot.Core.Models;

public class ExperimentAssignment
{
    public int AssignmentId { get; set; }
    public int ExperimentId { get; set; }
    public int SessionId { get; set; }
    public ContextFormat Format { get; set; }
    public ContextTier Tier { get; set; }
    public int? TokensUsed { get; set; }
    public int? TokenBudget { get; set; }
    public DateTime AssignedAt { get; set; }
}
