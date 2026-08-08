using Fewshot.Core.Models;

namespace Fewshot.Core.Interfaces;

/// <summary>
/// Manages semantic memory in Qdrant — embedding, retrieval, decay.
/// </summary>
public interface ISemanticMemoryService
{
    Task<List<SemanticMemory>> SearchAsync(string query, int topK = 5, double scoreThreshold = 0.72);
    Task UpsertAsync(int sessionId, string summary, string? solution, string? tags, string outcome, double importanceScore);
    Task DecayUnusedAsync(int daysThreshold = 30);
    Task PruneAsync(double minImportance = 0.15, int minAccessCount = 3);
    Task DeleteAsync(string pointId);
}
