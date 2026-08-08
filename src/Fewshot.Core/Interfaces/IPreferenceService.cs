using Fewshot.Core.Models;

namespace Fewshot.Core.Interfaces;

public interface IPreferenceService
{
    Task<Preference> UpsertAsync(Preference preference);
    Task<Preference> ReinforceOrUpsertAsync(string category, string key, string value, int sessionId);
    Task<List<Preference>> GetAllAsync();
    Task<List<Preference>> GetByCategoryAsync(string category);
    Task ReinforceAsync(int prefId);
    Task DeleteAsync(int prefId);
}
