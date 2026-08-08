using StarkTrace.Core.Models;

namespace StarkTrace.Core.Interfaces;

public interface IPreferenceRepository
{
    Task<Preference> UpsertAsync(Preference preference);
    Task<List<Preference>> GetAllAsync();
    Task<List<Preference>> GetByCategoryAsync(string category);
    Task ReinforceAsync(int prefId);
    Task DeleteAsync(int prefId);
}
