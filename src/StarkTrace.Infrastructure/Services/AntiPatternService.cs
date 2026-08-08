using StarkTrace.Core.Interfaces;
using StarkTrace.Core.Models;
using StarkTrace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace StarkTrace.Infrastructure.Services;

public class AntiPatternService : IAntiPatternService
{
    private readonly StarkTraceDbContext _db;

    public AntiPatternService(StarkTraceDbContext db) => _db = db;

    public async Task<AntiPattern> CreateAsync(AntiPattern ap)
    {
        _db.AntiPatterns.Add(ap);
        await _db.SaveChangesAsync();
        return ap;
    }

    public async Task<List<AntiPattern>> GetByLanguageAsync(string? language = null)
    {
        var query = _db.AntiPatterns.AsQueryable();

        if (language != null)
            query = query.Where(a => a.Language == language || a.Language == null);

        return await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
    }

    public async Task<List<AntiPattern>> GetAllAsync()
    {
        return await _db.AntiPatterns
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task DeleteAsync(int antiPatternId)
    {
        await _db.AntiPatterns
            .Where(a => a.AntiPatternId == antiPatternId)
            .ExecuteDeleteAsync();
    }
}
