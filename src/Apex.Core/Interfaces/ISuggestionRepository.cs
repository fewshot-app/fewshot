using Apex.Core.Models;

namespace Apex.Core.Interfaces;

public interface ISuggestionRepository
{
    Task<Suggestion> CreateAsync(Suggestion suggestion);
    Task<List<Suggestion>> GetBySessionAsync(int sessionId);
    Task MarkAppliedAsync(int suggestionId);
    Task<int> GetCountBySessionAsync(int sessionId);
    Task<int> GetAppliedCountBySessionAsync(int sessionId);
}
