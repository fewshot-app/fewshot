using Apex.Api.Jobs;
using Apex.Core.Interfaces;
using Apex.Core.Models;
using Apex.Infrastructure.Data;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _tasks;
    private readonly ITaskQueue _queue;
    private readonly IAgencyGate _gate;
    private readonly IBackgroundJobClient _jobs;

    public TasksController(ITaskService tasks, ITaskQueue queue, IAgencyGate gate, IBackgroundJobClient jobs)
    {
        _tasks = tasks;
        _queue = queue;
        _gate = gate;
        _jobs = jobs;
    }

    /// <summary>
    /// Create and enqueue a new task. Returns agency gate status if not ready.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateTaskRequest req)
    {
        // Check agency gate
        var readiness = await _gate.CheckReadinessAsync();
        if (!readiness.IsReady && !req.BypassGate)
        {
            return StatusCode(403, new
            {
                error = "Agency gate not ready",
                readiness
            });
        }

        var payload = req.Payload ?? "{}";
        if (req.BypassGate)
        {
            // Inject bypassGate into payload so the processor also skips the gate
            try
            {
                var doc = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(payload) ?? [];
                doc["bypassGate"] = true;
                payload = System.Text.Json.JsonSerializer.Serialize(doc);
            }
            catch { payload = "{\"bypassGate\":true}"; }
        }

        var task = await _tasks.CreateAsync(new ApexTask
        {
            SessionId = req.SessionId,
            TaskType = req.TaskType,
            Payload = payload,
            RequiresApproval = req.RequiresApproval,
            MaxAttempts = req.MaxAttempts ?? 3
        });

        // Enqueue to Redis
        await _queue.EnqueueAsync(task.TaskId, req.Priority);

        // Fire Hangfire job to process
        var jobId = _jobs.Enqueue<TaskProcessorJob>(j => j.ProcessAsync(task.TaskId));

        return CreatedAtAction(nameof(Get), new { taskId = task.TaskId }, new
        {
            task,
            jobId,
            queueDepth = await _queue.GetQueueDepthAsync()
        });
    }

    /// <summary>
    /// Get a task by ID.
    /// </summary>
    [HttpGet("{taskId}")]
    public async Task<ActionResult<ApexTask>> Get(int taskId)
    {
        var task = await _tasks.GetAsync(taskId);
        return task is null ? NotFound() : Ok(task);
    }

    /// <summary>
    /// Get all tasks for a session.
    /// </summary>
    [HttpGet("session/{sessionId}")]
    public async Task<ActionResult<List<ApexTask>>> GetBySession(int sessionId)
    {
        return Ok(await _tasks.GetBySessionAsync(sessionId));
    }

    /// <summary>
    /// Get tasks awaiting approval.
    /// </summary>
    [HttpGet("pending-approval")]
    public async Task<ActionResult<List<ApexTask>>> GetPendingApproval()
    {
        return Ok(await _tasks.GetAwaitingApprovalAsync());
    }

    /// <summary>
    /// Approve a task for execution.
    /// </summary>
    [HttpPost("{taskId}/approve")]
    public async Task<IActionResult> Approve(int taskId)
    {
        await _tasks.ApproveAsync(taskId);
        // Re-enqueue for processing
        await _queue.EnqueueAsync(taskId, "high");
        _jobs.Enqueue<TaskProcessorJob>(j => j.ProcessAsync(taskId));
        return Ok(new { taskId, status = "Approved and re-queued" });
    }

    /// <summary>
    /// Reject a task.
    /// </summary>
    [HttpPost("{taskId}/reject")]
    public async Task<IActionResult> Reject(int taskId, [FromBody] RejectRequest req)
    {
        await _tasks.RejectAsync(taskId, req.Reason);
        return Ok(new { taskId, status = "Rejected" });
    }

    /// <summary>
    /// Check agency readiness status.
    /// </summary>
    [HttpGet("readiness")]
    public async Task<ActionResult<AgencyReadiness>> CheckReadiness()
    {
        return Ok(await _gate.CheckReadinessAsync());
    }

    /// <summary>
    /// Get current queue depth.
    /// </summary>
    [HttpGet("queue-depth")]
    public async Task<ActionResult<object>> QueueDepth()
    {
        var depth = await _queue.GetQueueDepthAsync();
        return Ok(new { depth });
    }

    /// <summary>
    /// Get current agency gate thresholds.
    /// </summary>
    [HttpGet("thresholds")]
    public ActionResult<AgencyGateThresholdsDto> GetThresholds(
        [FromServices] AgencyGateOptions o)
    {
        return Ok(new AgencyGateThresholdsDto
        {
            MinSuggestions = o.MinSuggestions,
            MinFeedbackRate = o.MinFeedbackRate,
            MinAntiPatternSuppressions = o.MinAntiPatternSuppressions,
            MinConsolidatedSessions = o.MinConsolidatedSessions,
            RecommendedSuggestions = AgencyGateOptions.RecommendedSuggestions,
            RecommendedFeedbackRate = AgencyGateOptions.RecommendedFeedbackRate,
            RecommendedSuppressions = AgencyGateOptions.RecommendedSuppressions,
            RecommendedConsolidatedSessions = AgencyGateOptions.RecommendedConsolidatedSessions
        });
    }

    /// <summary>
    /// Update agency gate thresholds at runtime.
    /// </summary>
    [HttpPut("thresholds")]
    public async Task<ActionResult<AgencyGateThresholdsDto>> UpdateThresholds(
        [FromBody] UpdateThresholdsRequest req,
        [FromServices] AgencyGateOptions o,
        [FromServices] ApexDbContext db)
    {
        if (req.MinSuggestions.HasValue) o.MinSuggestions = req.MinSuggestions.Value;
        if (req.MinFeedbackRate.HasValue) o.MinFeedbackRate = req.MinFeedbackRate.Value;
        if (req.MinAntiPatternSuppressions.HasValue) o.MinAntiPatternSuppressions = req.MinAntiPatternSuppressions.Value;
        if (req.MinConsolidatedSessions.HasValue) o.MinConsolidatedSessions = req.MinConsolidatedSessions.Value;

        // Persist to DB
        await UpsertSettingAsync(db, "AgencyGate:MinSuggestions", o.MinSuggestions.ToString());
        await UpsertSettingAsync(db, "AgencyGate:MinFeedbackRate", o.MinFeedbackRate.ToString());
        await UpsertSettingAsync(db, "AgencyGate:MinAntiPatternSuppressions", o.MinAntiPatternSuppressions.ToString());
        await UpsertSettingAsync(db, "AgencyGate:MinConsolidatedSessions", o.MinConsolidatedSessions.ToString());

        return Ok(new AgencyGateThresholdsDto
        {
            MinSuggestions = o.MinSuggestions,
            MinFeedbackRate = o.MinFeedbackRate,
            MinAntiPatternSuppressions = o.MinAntiPatternSuppressions,
            MinConsolidatedSessions = o.MinConsolidatedSessions,
            RecommendedSuggestions = AgencyGateOptions.RecommendedSuggestions,
            RecommendedFeedbackRate = AgencyGateOptions.RecommendedFeedbackRate,
            RecommendedSuppressions = AgencyGateOptions.RecommendedSuppressions,
            RecommendedConsolidatedSessions = AgencyGateOptions.RecommendedConsolidatedSessions
        });
    }

    private static async Task UpsertSettingAsync(ApexDbContext db, string key, string value)
    {
        var existing = await db.SystemSettings.FindAsync(key);
        if (existing != null)
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            db.SystemSettings.Add(new Apex.Core.Models.SystemSetting
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }
}

public record CreateTaskRequest(
    int SessionId,
    string TaskType,
    string? Payload = null,
    bool RequiresApproval = false,
    string? Priority = null,
    int? MaxAttempts = 3,
    bool BypassGate = false);

public record RejectRequest(string Reason);

public class AgencyGateThresholdsDto
{
    public int MinSuggestions { get; set; }
    public double MinFeedbackRate { get; set; }
    public int MinAntiPatternSuppressions { get; set; }
    public int MinConsolidatedSessions { get; set; }
    public int RecommendedSuggestions { get; set; }
    public double RecommendedFeedbackRate { get; set; }
    public int RecommendedSuppressions { get; set; }
    public int RecommendedConsolidatedSessions { get; set; }
}

public record UpdateThresholdsRequest(
    int? MinSuggestions = null,
    double? MinFeedbackRate = null,
    int? MinAntiPatternSuppressions = null,
    int? MinConsolidatedSessions = null);
