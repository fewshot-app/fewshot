using StarkTrace.Core.Models;

namespace StarkTrace.Core.Interfaces;

public interface IAntiPatternService
{
    Task<AntiPattern> CreateAsync(AntiPattern antiPattern);
    Task<List<AntiPattern>> GetByLanguageAsync(string? language = null);
    Task<List<AntiPattern>> GetAllAsync();
    Task DeleteAsync(int antiPatternId);
}
