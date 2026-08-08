using StarkTrace.Core.Models;

namespace StarkTrace.Core.Interfaces;

/// <summary>
/// Orchestrates the audit pipeline: regex → Presidio → entropy.
/// Sits between context assembly and the Claude API call.
/// </summary>
public interface IAuditService
{
    Task<AuditPipelineResult> AnalyzeAsync(string content, int sessionId);
}
