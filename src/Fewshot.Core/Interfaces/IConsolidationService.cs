namespace Fewshot.Core.Interfaces;

/// <summary>
/// Nightly consolidation job — the "sleep cycle."
/// Processes completed sessions into structured memory.
/// </summary>
public interface IConsolidationService
{
    Task RunAsync();
}
