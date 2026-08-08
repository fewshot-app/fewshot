namespace Fewshot.Core.Enums;

public enum TaskStatus
{
    Queued,
    Analyzing,
    Executing,
    Verifying,
    AwaitingApproval,
    Completed,
    Failed
}
