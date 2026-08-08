using Fewshot.Core.Models;

namespace Fewshot.Core.Interfaces;

/// <summary>
/// Extracts suggestions from Claude's response messages (async background job).
/// </summary>
public interface ISuggestionExtractor
{
    Task<List<Suggestion>> ExtractAsync(int messageId, string content);
}
