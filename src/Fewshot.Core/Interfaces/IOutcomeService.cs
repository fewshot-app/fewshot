using Fewshot.Core.Models;
using Fewshot.Core.Enums;

namespace Fewshot.Core.Interfaces;

public interface IOutcomeService
{
    Task<Outcome> RecordAsync(Outcome outcome);
    Task<List<Outcome>> GetBySuggestionAsync(int suggestionId);
    Task<(int Worked, int Failed)> GetCountsBySessionAsync(int sessionId);
    Task<int> GetEffortSavedBySessionAsync(int sessionId);
}
