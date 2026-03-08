using Apex.Core.Models;

namespace Apex.Core.Interfaces;

public interface ITaskStepRepository
{
    Task<TaskStep> CreateAsync(TaskStep step);
    Task<List<TaskStep>> GetByTaskAsync(int taskId);
    Task UpdateStatusAsync(int stepId, Enums.TaskStepStatus status, string? output = null);
    Task<TaskStep?> GetLastCompletedAsync(int taskId);
}
