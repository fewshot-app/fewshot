using Fewshot.Core.Models;

namespace Fewshot.Core.Interfaces;

public interface ISuggestionService
{
    Task<Suggestion> CreateAsync(Suggestion suggestion);
    Task<List<Suggestion>> GetBySessionAsync(int sessionId);
    Task MarkAppliedAsync(int suggestionId);
    Task<int> GetCountBySessionAsync(int sessionId);
    Task<int> GetAppliedCountBySessionAsync(int sessionId);
}
