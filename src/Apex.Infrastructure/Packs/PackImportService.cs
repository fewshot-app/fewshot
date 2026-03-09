using Apex.Core.Interfaces;
using Apex.Core.Models;
using Apex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Apex.Infrastructure.Packs;

/// <summary>
/// Imports packs: decrypt → parse → embed memories → bulk insert.
/// Handles both encrypted (licensed) and unencrypted (local export) packs.
/// </summary>
public class PackImportService
{
    private readonly ApexDbContext _db;
    private readonly IMemoryService _memory;
    private readonly ILogger<PackImportService> _log;

    public PackImportService(ApexDbContext db, IMemoryService memory, ILogger<PackImportService> log)
    {
        _db = db;
        _memory = memory;
        _log = log;
    }

    /// <summary>
    /// Import from unencrypted ApexPack JSON (local export or dev use).
    /// </summary>
    public async Task<PackImportResult> ImportFromJsonAsync(string json, string? targetProject = null)
    {
        ApexPack pack;
        try
        {
            pack = PackCrypto.DeserializePack(json);
        }
        catch (Exception ex)
        {
            return new PackImportResult { Success = false, Error = $"Invalid pack JSON: {ex.Message}" };
        }

        return await ImportPackAsync(pack, targetProject);
    }

    /// <summary>
    /// Import from encrypted .apexpack envelope with a decryption key.
    /// </summary>
    public async Task<PackImportResult> ImportEncryptedAsync(string envelopeJson, string decryptionKey, string? targetProject = null)
    {
        ApexPack pack;
        try
        {
            var envelope = PackCrypto.DeserializeEnvelope(envelopeJson);
            pack = PackCrypto.Decrypt(envelope, decryptionKey);
        }
        catch (Exception ex)
        {
            return new PackImportResult { Success = false, Error = $"Decryption failed: {ex.Message}" };
        }

        return await ImportPackAsync(pack, targetProject);
    }

    /// <summary>
    /// Core import logic — embeds memories, inserts preferences and anti-patterns.
    /// </summary>
    private async Task<PackImportResult> ImportPackAsync(ApexPack pack, string? targetProject)
    {
        var project = targetProject ?? pack.TargetProject;
        var result = new PackImportResult
        {
            PackId = pack.PackId,
            PackName = pack.Name
        };

        _log.LogInformation("Importing pack '{PackName}' ({PackId}) — {Mem} memories, {Pref} prefs, {Ap} anti-patterns",
            pack.Name, pack.PackId, pack.Memories.Count, pack.Preferences.Count, pack.AntiPatterns.Count);

        // Ensure the target project exists
        var dbProject = await _db.Projects.FirstOrDefaultAsync(p => p.Name == project);
        if (dbProject == null)
        {
            _log.LogWarning("Project '{Project}' not found — creating it", project);
            dbProject = new Project
            {
                Name = project,
                DisplayName = pack.Name,
                Keywords = project,
                IsActive = true
            };
            _db.Projects.Add(dbProject);
            await _db.SaveChangesAsync();
        }

        // Import memories (embed each one via IMemoryService)
        foreach (var mem in pack.Memories)
        {
            try
            {
                var isDuplicate = await _memory.IsDuplicateAsync(mem.Summary);
                if (isDuplicate)
                {
                    result.DuplicatesSkipped++;
                    continue;
                }

                var stored = await _memory.StoreAsync(new MemoryStoreRequest
                {
                    SessionId = 0, // Pack imports don't have a session
                    Summary = mem.Summary,
                    Solution = mem.Solution,
                    Approach = mem.Approach,
                    OutcomeLabel = mem.OutcomeLabel,
                    Tags = mem.Tags,
                    Language = mem.Language,
                    Project = project
                });

                if (stored != null) result.MemoriesImported++;
                else result.DuplicatesSkipped++;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to import memory: {Summary}", mem.Summary[..Math.Min(80, mem.Summary.Length)]);
            }
        }

        // Import preferences (upsert by Category+Key)
        foreach (var pref in pack.Preferences)
        {
            try
            {
                var existing = await _db.Preferences
                    .FirstOrDefaultAsync(p => p.Category == pref.Category && p.Key == pref.Key);

                if (existing != null)
                {
                    // Only overwrite if pack confidence is higher
                    if (pref.ConfidenceScore > existing.ConfidenceScore)
                    {
                        existing.Value = pref.Value;
                        existing.ConfidenceScore = pref.ConfidenceScore;
                        existing.LastUpdated = DateTime.UtcNow;
                        result.PreferencesImported++;
                    }
                    else
                    {
                        result.DuplicatesSkipped++;
                    }
                }
                else
                {
                    _db.Preferences.Add(new Preference
                    {
                        Category = pref.Category,
                        Key = pref.Key,
                        Value = pref.Value,
                        ConfidenceScore = pref.ConfidenceScore,
                        ReinforcementCount = 1,
                        IsExplicit = false,
                        LastUpdated = DateTime.UtcNow
                    });
                    result.PreferencesImported++;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to import preference: {Cat}/{Key}", pref.Category, pref.Key);
            }
        }

        // Import anti-patterns (skip exact duplicates)
        foreach (var ap in pack.AntiPatterns)
        {
            try
            {
                var exists = await _db.AntiPatterns
                    .AnyAsync(x => x.Pattern == ap.Pattern && x.Reason == ap.Reason);

                if (exists)
                {
                    result.DuplicatesSkipped++;
                    continue;
                }

                _db.AntiPatterns.Add(new AntiPattern
                {
                    SessionId = 0,
                    Pattern = ap.Pattern,
                    Reason = ap.Reason,
                    Language = ap.Language,
                    ErrorCode = ap.ErrorCode,
                    CreatedAt = DateTime.UtcNow
                });
                result.AntiPatternsImported++;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to import anti-pattern: {Pattern}", ap.Pattern[..Math.Min(80, ap.Pattern.Length)]);
            }
        }

        await _db.SaveChangesAsync();

        result.Success = true;
        _log.LogInformation("Pack import complete: {Mem} memories, {Pref} prefs, {Ap} anti-patterns, {Dup} skipped",
            result.MemoriesImported, result.PreferencesImported, result.AntiPatternsImported, result.DuplicatesSkipped);

        return result;
    }
}
