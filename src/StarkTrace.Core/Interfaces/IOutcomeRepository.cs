using StarkTrace.Core.Enums;
using StarkTrace.Core.Models;

namespace StarkTrace.Core.Interfaces;

public interface IOutcomeRepository
{
    Task<Outcome> CreateAsync(Outcome outcome);
    Task<Outcome?> GetBySuggestionAsync(int suggestionId);
    Task UpdateStatusAsync(int outcomeId, OutcomeStatus status, string? notes = null);
    Task MarkGitConfirmedAsync(int outcomeId);
    Task<int> GetWorkedCountBySessionAsync(int sessionId);
    Task<int> GetFailedCountBySessionAsync(int sessionId);
    Task<int?> GetTotalEffortSavedBySessionAsync(int sessionId);
}
