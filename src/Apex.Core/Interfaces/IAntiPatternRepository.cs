using Apex.Core.Models;

namespace Apex.Core.Interfaces;

public interface IAntiPatternRepository
{
    Task<AntiPattern> CreateAsync(AntiPattern antiPattern);
    Task<List<AntiPattern>> GetByLanguageAsync(string? language = null);
    Task<List<AntiPattern>> GetByProjectAsync(string project);
    Task DeleteAsync(int antiPatternId);
}
