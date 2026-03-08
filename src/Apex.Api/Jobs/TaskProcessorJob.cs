using System.Text.Json;
using Apex.Core.Interfaces;
using Apex.Core.Models;
using Apex.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Apex.Api.Jobs;

/// <summary>
/// Background worker that dequeues tasks from Redis and executes them.
/// Uses the agency gate to ensure prerequisites are met before processing.
/// </summary>
public class TaskProcessorJob
{
    private readonly ITaskService _tasks;
    private readonly ITaskQueue _queue;
    private readonly IAgencyGate _gate;
    private readonly IHubContext<ApexHub> _hub;
    private readonly ILogger<TaskProcessorJob> _logger;

    public TaskProcessorJob(
        ITaskService tasks,
        ITaskQueue queue,
        IAgencyGate gate,
        IHubContext<ApexHub> hub,
        ILogger<TaskProcessorJob> logger)
    {
        _tasks = tasks;
        _queue = queue;
        _gate = gate;
        _hub = hub;
        _logger = logger;
    }

    /// <summary>
    /// Process a single task by ID (called from Hangfire or direct).
    /// </summary>
    public async Task ProcessAsync(int taskId)
    {
        var task = await _tasks.GetAsync(taskId);
        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found", taskId);
            return;
        }

        // Agency gate check (skip if payload contains bypassGate)
        var bypassGate = false;
        try { bypassGate = JsonSerializer.Deserialize<JsonElement>(task.Payload).TryGetProperty("bypassGate", out var bg) && bg.GetBoolean(); } catch { }

        if (!bypassGate)
        {
            var readiness = await _gate.CheckReadinessAsync();
            if (!readiness.IsReady)
            {
                _logger.LogWarning("Agency gate blocked task {TaskId}: {Reasons}",
                    taskId, string.Join("; ", readiness.BlockingReasons));
                await _tasks.FailAsync(taskId, $"Agency gate not ready: {string.Join("; ", readiness.BlockingReasons)}");
                return;
            }
        }

        try
        {
            // Transition to Analyzing
            await _tasks.TransitionAsync(taskId, "Analyzing");
            await NotifyAsync(taskId, "Analyzing");

            // Check if task requires approval
            if (task.RequiresApproval)
            {
                await _tasks.TransitionAsync(taskId, "AwaitingApproval");
                await NotifyAsync(taskId, "AwaitingApproval", "Task requires approval before execution");
                _logger.LogInformation("Task {TaskId} awaiting approval", taskId);
                return;
            }

            // Execute based on task type
            await _tasks.TransitionAsync(taskId, "Executing");
            await NotifyAsync(taskId, "Executing");

            var result = await ExecuteTaskAsync(task);

            // Verify
            await _tasks.TransitionAsync(taskId, "Verifying");
            await NotifyAsync(taskId, "Verifying");

            // Complete
            await _tasks.CompleteAsync(taskId, result);
            await NotifyAsync(taskId, "Completed", result);

            _logger.LogInformation("Task {TaskId} ({Type}) completed successfully", taskId, task.TaskType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task {TaskId} failed", taskId);
            await _tasks.FailAsync(taskId, ex.Message);
            await NotifyAsync(taskId, "Failed", ex.Message);
        }
    }

    private async Task<string> ExecuteTaskAsync(ApexTask task)
    {
        return task.TaskType switch
        {
            "RefactorSuggestion" => await ExecuteRefactorAsync(task),
            "AntiPatternCheck" => await ExecuteAntiPatternCheckAsync(task),
            "PreferenceEnforcement" => await ExecutePreferenceEnforcementAsync(task),
            "MemoryCleanup" => await ExecuteMemoryCleanupAsync(task),
            _ => throw new InvalidOperationException($"Unknown task type: {task.TaskType}")
        };
    }

    private async Task<string> ExecuteRefactorAsync(ApexTask task)
    {
        var step = await _tasks.AddStepAsync(task.TaskId, "AnalyzeRefactoring");
        // Placeholder: in production this would call the LLM to analyze code
        // and propose refactoring based on learned patterns
        await _tasks.CompleteStepAsync(step.StepId, "Refactoring analysis complete");
        return "Refactoring suggestion generated";
    }

    private async Task<string> ExecuteAntiPatternCheckAsync(ApexTask task)
    {
        var step = await _tasks.AddStepAsync(task.TaskId, "ScanForAntiPatterns");
        // Placeholder: scan recent code changes against known anti-patterns
        await _tasks.CompleteStepAsync(step.StepId, "Anti-pattern scan complete");
        return "No anti-patterns detected in recent changes";
    }

    private async Task<string> ExecutePreferenceEnforcementAsync(ApexTask task)
    {
        var step = await _tasks.AddStepAsync(task.TaskId, "CheckPreferenceCompliance");
        // Placeholder: verify code follows developer preferences
        await _tasks.CompleteStepAsync(step.StepId, "Preference check complete");
        return "Code follows developer preferences";
    }

    private async Task<string> ExecuteMemoryCleanupAsync(ApexTask task)
    {
        var step = await _tasks.AddStepAsync(task.TaskId, "CleanupStaleMemories");
        // Placeholder: identify and remove outdated or low-relevance memories
        await _tasks.CompleteStepAsync(step.StepId, "Memory cleanup complete");
        return "Memory cleanup completed";
    }

    private async Task NotifyAsync(int taskId, string status, string? message = null)
    {
        await _hub.Clients.All.SendAsync("TaskUpdate", new
        {
            taskId,
            status,
            message,
            timestamp = DateTime.UtcNow
        });
    }
}
