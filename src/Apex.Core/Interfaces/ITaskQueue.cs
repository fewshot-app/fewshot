namespace Apex.Core.Interfaces;

/// <summary>
/// Redis-backed task queue using BLPOP for zero-polling dispatch.
/// </summary>
public interface ITaskQueue
{
    /// <summary>
    /// Enqueue a task ID for processing.
    /// </summary>
    Task EnqueueAsync(int taskId, string? priority = null);

    /// <summary>
    /// Dequeue the next task ID. Blocks up to timeout seconds.
    /// Returns null if timeout expires with no task.
    /// </summary>
    Task<int?> DequeueAsync(TimeSpan? timeout = null);

    /// <summary>
    /// Get the current queue depth.
    /// </summary>
    Task<long> GetQueueDepthAsync();
}
