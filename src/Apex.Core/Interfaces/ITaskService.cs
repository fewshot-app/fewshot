using Apex.Core.Models;

namespace Apex.Core.Interfaces;

/// <summary>
/// Manages task lifecycle: creation, state transitions, step tracking.
/// </summary>
public interface ITaskService
{
    Task<ApexTask> CreateAsync(ApexTask task);
    Task<ApexTask?> GetAsync(int taskId);
    Task<List<ApexTask>> GetBySessionAsync(int sessionId);
    Task<List<ApexTask>> GetPendingAsync(int limit = 10);
    Task<List<ApexTask>> GetAwaitingApprovalAsync();
    Task TransitionAsync(int taskId, string newStatus);
    Task<ApexTask?> LockNextAsync(string workerName);
    Task ReleaseAsync(int taskId);
    Task CompleteAsync(int taskId, string? result = null);
    Task FailAsync(int taskId, string error);
    Task<TaskStep> AddStepAsync(int taskId, string stepName);
    Task CompleteStepAsync(int stepId, string? output = null);
    Task FailStepAsync(int stepId, string? output = null);
    Task ApproveAsync(int taskId);
    Task RejectAsync(int taskId, string reason);
}
