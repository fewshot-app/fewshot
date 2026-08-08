using StarkTrace.Core.Interfaces;
using StarkTrace.Core.Models;
using StarkTrace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace StarkTrace.Infrastructure.Services;

public class PreferenceService : IPreferenceService
{
    private readonly StarkTraceDbContext _db;

    public PreferenceService(StarkTraceDbContext db) => _db = db;

    public async Task<Preference> UpsertAsync(Preference p)
    {
        var existing = await _db.Preferences
            .FirstOrDefaultAsync(x => x.Category == p.Category && x.Key == p.Key);

        if (existing != null)
        {
            existing.Value = p.Value;
            existing.ConfidenceScore = p.ConfidenceScore;
            existing.IsExplicit = p.IsExplicit;
            existing.LastUpdated = DateTime.Now;
            await _db.SaveChangesAsync();
            p.PrefId = existing.PrefId;
        }
        else
        {
            _db.Preferences.Add(p);
            await _db.SaveChangesAsync();
        }

        return p;
    }

    public async Task<List<Preference>> GetAllAsync()
    {
        return await _db.Preferences
            .OrderByDescending(p => p.ConfidenceScore)
            .ToListAsync();
    }

    public async Task<List<Preference>> GetByCategoryAsync(string category)
    {
        return await _db.Preferences
            .Where(p => p.Category == category)
            .OrderByDescending(p => p.ConfidenceScore)
            .ToListAsync();
    }

    public async Task<Preference> ReinforceOrUpsertAsync(string category, string key, string value, int sessionId)
    {
        var existing = await _db.Preferences
            .FirstOrDefaultAsync(x => x.Category == category && x.Key == key);

        if (existing != null)
        {
            var valueChanged = !string.Equals(existing.Value, value, StringComparison.OrdinalIgnoreCase);

            existing.ReinforcementCount++;
            existing.SourceSessionId = sessionId;
            existing.LastUpdated = DateTime.Now;

            if (valueChanged)
            {
                // Preference evolved — update value but don't fully trust it yet
                existing.Value = value;
                existing.ConfidenceScore = Math.Max(0.4, existing.ConfidenceScore - 0.1);
            }
            else
            {
                // Same preference seen again — reinforce confidence asymptotically toward 0.95
                existing.ConfidenceScore = Math.Min(0.95, 0.5 + existing.ReinforcementCount * 0.1);
            }

            await _db.SaveChangesAsync();
            return existing;
        }
        else
        {
            var pref = new Preference
            {
                Category = category,
                Key = key,
                Value = value,
                IsExplicit = false,
                ConfidenceScore = 0.5,
                ReinforcementCount = 1,
                SourceSessionId = sessionId,
                LastUpdated = DateTime.Now
            };
            _db.Preferences.Add(pref);
            await _db.SaveChangesAsync();
            return pref;
        }
    }

    public async Task ReinforceAsync(int prefId)
    {
        var pref = await _db.Preferences.FindAsync(prefId);
        if (pref == null) return;

        pref.ReinforcementCount++;
        pref.ConfidenceScore = Math.Min(0.95, 0.5 + pref.ReinforcementCount * 0.1);
        pref.LastUpdated = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int prefId)
    {
        await _db.Preferences
            .Where(p => p.PrefId == prefId)
            .ExecuteDeleteAsync();
    }
}
