using Apex.Core.Enums;
using Apex.Core.Models;

namespace Apex.Core.Interfaces;

/// <summary>
/// Assembles the context window from all five priority tiers
/// using the assigned format (ACL or Prose) per tier.
/// </summary>
public interface IContextInjector
{
    /// <summary>
    /// Build context from fully pre-populated inputs (used by compare.sh, tests).
    /// </summary>
    Task<ContextInjectionResult> BuildContextAsync(int sessionId, ContextInputs inputs);

    /// <summary>
    /// Build context by auto-hydrating P2-P4 from Qdrant + SQL.
    /// Only requires P1 state and P5 facts from the caller.
    /// </summary>
    Task<ContextInjectionResult> BuildContextAutoAsync(int sessionId, CurrentStateContext state, ProjectFacts? facts = null);
}
