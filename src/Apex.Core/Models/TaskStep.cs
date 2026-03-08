using Apex.Core.Enums;

namespace Apex.Core.Models;

public class TaskStep
{
    public int StepId { get; set; }
    public int TaskId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public TaskStepStatus Status { get; set; }
    public string? FilePath { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Output { get; set; }
}
