using Apex.Core.Models;

namespace Apex.Core.Interfaces;

public interface ITaskRepository
{
    Task<ApexTask> CreateAsync(ApexTask task);
    Task<ApexTask?> GetAsync(int taskId);
    Task<ApexTask?> DequeueAsync(string workerId);
    Task UpdateStatusAsync(int taskId, string status, string? result = null);
    Task IncrementAttemptAsync(int taskId, string error, DateTime? nextRetry = null);
    Task<List<ApexTask>> GetBySessionAsync(int sessionId);
    Task ReleaseStaleLocks(TimeSpan maxLockAge);
}
