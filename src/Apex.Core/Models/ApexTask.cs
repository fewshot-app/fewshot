namespace Apex.Core.Models;

public class ApexTask
{
    public int TaskId { get; set; }
    public int SessionId { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued";
    public string Payload { get; set; } = string.Empty;
    public string? Result { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public string? LastError { get; set; }
    public string? LockedBy { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public bool RequiresApproval { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
