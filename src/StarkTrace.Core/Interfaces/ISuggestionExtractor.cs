using StarkTrace.Core.Models;

namespace StarkTrace.Core.Interfaces;

/// <summary>
/// Extracts suggestions from Claude's response messages (async background job).
/// </summary>
public interface ISuggestionExtractor
{
    Task<List<Suggestion>> ExtractAsync(int messageId, string content);
}
