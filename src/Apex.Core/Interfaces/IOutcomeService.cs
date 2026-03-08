using Apex.Core.Models;
using Apex.Core.Enums;

namespace Apex.Core.Interfaces;

public interface IOutcomeService
{
    Task<Outcome> RecordAsync(Outcome outcome);
    Task<List<Outcome>> GetBySuggestionAsync(int suggestionId);
    Task<(int Worked, int Failed)> GetCountsBySessionAsync(int sessionId);
    Task<int> GetEffortSavedBySessionAsync(int sessionId);
}
