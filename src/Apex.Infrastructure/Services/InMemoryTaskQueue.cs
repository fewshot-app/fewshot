using System.Threading.Channels;
using Apex.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Apex.Infrastructure.Services;

/// <summary>
/// In-process task queue using System.Threading.Channels.
/// Priority tasks go to the front via a separate high-priority channel.
/// No external dependencies — drop-in replacement for RedisTaskQueue.
/// </summary>
public class InMemoryTaskQueue : ITaskQueue
{
    private readonly Channel<int> _priority = Channel.CreateUnbounded<int>();
    private readonly Channel<int> _standard = Channel.CreateUnbounded<int>();
    private readonly ILogger<InMemoryTaskQueue> _logger;

    public InMemoryTaskQueue(ILogger<InMemoryTaskQueue> logger) => _logger = logger;

    public async Task EnqueueAsync(int taskId, string? priority = null)
    {
        var channel = priority == "high" ? _priority : _standard;
        await channel.Writer.WriteAsync(taskId);
        _logger.LogInformation("Enqueued task {TaskId} [{Priority}]", taskId, priority ?? "standard");
    }

    public async Task<int?> DequeueAsync(TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));

        // Drain priority channel first
        if (_priority.Reader.TryRead(out var priorityTask))
        {
            _logger.LogInformation("Dequeued priority task {TaskId}", priorityTask);
            return priorityTask;
        }

        // Wait on either channel
        try
        {
            var priorityRead = _priority.Reader.WaitToReadAsync(cts.Token).AsTask();
            var standardRead = _standard.Reader.WaitToReadAsync(cts.Token).AsTask();

            var completed = await Task.WhenAny(priorityRead, standardRead);

            if (completed == priorityRead && _priority.Reader.TryRead(out var pt))
            {
                _logger.LogInformation("Dequeued priority task {TaskId}", pt);
                return pt;
            }

            if (_standard.Reader.TryRead(out var st))
            {
                _logger.LogInformation("Dequeued standard task {TaskId}", st);
                return st;
            }
        }
        catch (OperationCanceledException) { /* timeout — normal */ }

        return null;
    }

    public Task<long> GetQueueDepthAsync() =>
        Task.FromResult((long)(_priority.Reader.Count + _standard.Reader.Count));
}
