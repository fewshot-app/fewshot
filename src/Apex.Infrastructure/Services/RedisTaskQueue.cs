using Apex.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Apex.Infrastructure.Services;

/// <summary>
/// Redis-backed task queue using BLPOP for zero-polling dispatch.
/// Tasks are pushed to a list, workers BLPOP to dequeue.
/// </summary>
public class RedisTaskQueue : ITaskQueue
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisTaskQueue> _logger;
    private const string QueueKey = "apex:tasks:queue";
    private const string PriorityQueueKey = "apex:tasks:priority";

    public RedisTaskQueue(IConnectionMultiplexer redis, ILogger<RedisTaskQueue> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task EnqueueAsync(int taskId, string? priority = null)
    {
        var db = _redis.GetDatabase();
        var key = priority == "high" ? PriorityQueueKey : QueueKey;
        await db.ListRightPushAsync(key, taskId.ToString());
        _logger.LogInformation("Enqueued task {TaskId} to {Queue}", taskId, key);
    }

    public async Task<int?> DequeueAsync(TimeSpan? timeout = null)
    {
        var db = _redis.GetDatabase();
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);

        // Try priority queue first, then standard queue
        // BLPOP blocks until an item is available or timeout expires
        var result = await db.ExecuteAsync("BLPOP", PriorityQueueKey, QueueKey,
            (int)effectiveTimeout.TotalSeconds);

        if (result.IsNull)
            return null;

        // BLPOP returns [key, value]
        var arr = (RedisResult[])result!;
        if (arr.Length >= 2 && int.TryParse(arr[1].ToString(), out var taskId))
        {
            _logger.LogInformation("Dequeued task {TaskId} from {Queue}", taskId, arr[0].ToString());
            return taskId;
        }

        return null;
    }

    public async Task<long> GetQueueDepthAsync()
    {
        var db = _redis.GetDatabase();
        var standard = await db.ListLengthAsync(QueueKey);
        var priority = await db.ListLengthAsync(PriorityQueueKey);
        return standard + priority;
    }
}
