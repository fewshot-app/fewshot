using StarkTrace.Core.Models;
using StarkTrace.Core.Enums;

namespace StarkTrace.Core.Interfaces;

public interface IOutcomeService
{
    Task<Outcome> RecordAsync(Outcome outcome);
    Task<List<Outcome>> GetBySuggestionAsync(int suggestionId);
    Task<(int Worked, int Failed)> GetCountsBySessionAsync(int sessionId);
    Task<int> GetEffortSavedBySessionAsync(int sessionId);
}
