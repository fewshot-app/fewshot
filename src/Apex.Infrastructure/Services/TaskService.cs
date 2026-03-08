using Apex.Core.Interfaces;
using Apex.Core.Models;
using Apex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Apex.Infrastructure.Services;

public class TaskService : ITaskService
{
    private readonly ApexDbContext _db;

    public TaskService(ApexDbContext db) => _db = db;

    public async Task<ApexTask> CreateAsync(ApexTask task)
    {
        task.CreatedAt = DateTime.UtcNow;
        task.Status = "Queued";
        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    public async Task<ApexTask?> GetAsync(int taskId)
    {
        return await _db.Tasks.FindAsync(taskId);
    }

    public async Task<List<ApexTask>> GetBySessionAsync(int sessionId)
    {
        return await _db.Tasks
            .Where(t => t.SessionId == sessionId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<ApexTask>> GetPendingAsync(int limit = 10)
    {
        return await _db.Tasks
            .Where(t => t.Status == "Queued" && t.LockedBy == null)
            .OrderBy(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<ApexTask>> GetAwaitingApprovalAsync()
    {
        return await _db.Tasks
            .Where(t => t.Status == "AwaitingApproval")
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task TransitionAsync(int taskId, string newStatus)
    {
        await _db.Tasks
            .Where(t => t.TaskId == taskId)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.Status, newStatus));
    }

    public async Task<ApexTask?> LockNextAsync(string workerName)
    {
        // Atomic lock: find first unlocked queued task and claim it
        var task = await _db.Tasks
            .Where(t => t.Status == "Queued" && t.LockedBy == null)
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (task == null) return null;

        task.LockedBy = workerName;
        task.LockedAt = DateTime.UtcNow;
        task.Status = "Analyzing";
        task.StartedAt = DateTime.UtcNow;
        task.AttemptCount++;
        await _db.SaveChangesAsync();

        return task;
    }

    public async Task ReleaseAsync(int taskId)
    {
        await _db.Tasks
            .Where(t => t.TaskId == taskId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(t => t.LockedBy, (string?)null)
                .SetProperty(t => t.LockedAt, (DateTime?)null)
                .SetProperty(t => t.Status, "Queued"));
    }

    public async Task CompleteAsync(int taskId, string? result = null)
    {
        await _db.Tasks
            .Where(t => t.TaskId == taskId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(t => t.Status, "Completed")
                .SetProperty(t => t.Result, result)
                .SetProperty(t => t.CompletedAt, DateTime.UtcNow)
                .SetProperty(t => t.LockedBy, (string?)null));
    }

    public async Task FailAsync(int taskId, string error)
    {
        var task = await _db.Tasks.FindAsync(taskId);
        if (task == null) return;

        task.LastError = error;
        task.LockedBy = null;
        task.LockedAt = null;

        if (task.AttemptCount >= task.MaxAttempts)
        {
            task.Status = "Failed";
        }
        else
        {
            // Exponential backoff: 30s, 2min, 8min
            var delay = TimeSpan.FromSeconds(30 * Math.Pow(4, task.AttemptCount - 1));
            task.Status = "Queued";
            task.NextRetryAt = DateTime.UtcNow + delay;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<TaskStep> AddStepAsync(int taskId, string stepName)
    {
        var step = new TaskStep
        {
            TaskId = taskId,
            StepName = stepName,
            Status = Apex.Core.Enums.TaskStepStatus.Processing,
            StartedAt = DateTime.UtcNow
        };
        _db.TaskSteps.Add(step);
        await _db.SaveChangesAsync();
        return step;
    }

    public async Task CompleteStepAsync(int stepId, string? output = null)
    {
        await _db.TaskSteps
            .Where(s => s.StepId == stepId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(s => s.Status, Apex.Core.Enums.TaskStepStatus.Completed)
                .SetProperty(s => s.Output, output)
                .SetProperty(s => s.CompletedAt, DateTime.UtcNow));
    }

    public async Task FailStepAsync(int stepId, string? output = null)
    {
        await _db.TaskSteps
            .Where(s => s.StepId == stepId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(s => s.Status, Apex.Core.Enums.TaskStepStatus.Failed)
                .SetProperty(s => s.Output, output)
                .SetProperty(s => s.CompletedAt, DateTime.UtcNow));
    }

    public async Task ApproveAsync(int taskId)
    {
        await _db.Tasks
            .Where(t => t.TaskId == taskId && t.Status == "AwaitingApproval")
            .ExecuteUpdateAsync(x => x
                .SetProperty(t => t.Status, "Queued")
                .SetProperty(t => t.RequiresApproval, false));
    }

    public async Task RejectAsync(int taskId, string reason)
    {
        await _db.Tasks
            .Where(t => t.TaskId == taskId && t.Status == "AwaitingApproval")
            .ExecuteUpdateAsync(x => x
                .SetProperty(t => t.Status, "Failed")
                .SetProperty(t => t.Result, $"Rejected: {reason}"));
    }
}
