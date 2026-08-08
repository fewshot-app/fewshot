using StarkTrace.Core.Enums;
using StarkTrace.Core.Models;

namespace StarkTrace.Core.Interfaces;

/// <summary>
/// Manages A/B experiment lifecycle: assignment, metrics collection, analysis.
/// </summary>
public interface IExperimentService
{
    Task<Experiment> CreateAsync(string name, ContextTier tier, int targetSessions = 60);
    Task<SessionFormatPlan> AssignFormatsAsync(int sessionId);
    Task RecordTokenUsageAsync(int sessionId, List<ContextSegment> segments);
    Task CollectMetricsAsync(int sessionId);
    Task<List<ExperimentResultSummary>> GetResultsAsync();
    Task<List<ExperimentTokenSummary>> GetTokenResultsAsync();
    Task<List<ExperimentVerdict>> GetVerdictsAsync();
    Task ConcludeAsync(int experimentId, ContextFormat winner, string conclusion);
    Task PauseAsync(int experimentId);
    Task ResumeAsync(int experimentId);
}
