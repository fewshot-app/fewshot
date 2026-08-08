using StarkTrace.Core.Models;
using StarkTrace.Infrastructure.Packs;
using Microsoft.AspNetCore.Mvc;

namespace StarkTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PacksController : ControllerBase
{
    private readonly PackImportService _import;
    private readonly PackExportService _export;

    public PacksController(PackImportService import, PackExportService export)
    {
        _import = import;
        _export = export;
    }

    /// <summary>
    /// Export a project's knowledge as an unencrypted StarkTracePack JSON.
    /// </summary>
    [HttpGet("export/{project}")]
    public async Task<ActionResult<StarkTracePack>> Export(string project, [FromQuery] string? author = null)
    {
        var pack = await _export.ExportAsync(project, author);
        if (pack == null) return NotFound($"Project '{project}' not found.");
        return Ok(pack);
    }

    /// <summary>
    /// Export and encrypt a project's knowledge as an .apexpack envelope.
    /// </summary>
    [HttpGet("export/{project}/encrypted")]
    public async Task<ActionResult<EncryptedPackEnvelope>> ExportEncrypted(
        string project, [FromQuery] string key, [FromQuery] string? author = null)
    {
        if (string.IsNullOrEmpty(key)) return BadRequest("Encryption key is required.");

        var pack = await _export.ExportAsync(project, author);
        if (pack == null) return NotFound($"Project '{project}' not found.");

        try
        {
            var envelope = PackCrypto.Encrypt(pack, key);
            return Ok(envelope);
        }
        catch (Exception ex)
        {
            return BadRequest($"Encryption failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Import an unencrypted StarkTracePack JSON into StarkTrace.
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult<PackImportResult>> Import([FromBody] PackImportRequest request)
    {
        PackImportResult result;

        if (!string.IsNullOrEmpty(request.DecryptionKey))
        {
            result = await _import.ImportEncryptedAsync(request.PackJson, request.DecryptionKey, request.TargetProject);
        }
        else
        {
            result = await _import.ImportFromJsonAsync(request.PackJson, request.TargetProject);
        }

        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Generate a new encryption key for pack creation.
    /// </summary>
    [HttpGet("keygen")]
    public ActionResult<KeygenResult> Keygen()
    {
        return Ok(new KeygenResult(PackCrypto.GenerateKey()));
    }

    /// <summary>
    /// Get this machine's ID for license activation.
    /// </summary>
    [HttpGet("machine-id")]
    public ActionResult<MachineIdResult> GetMachineId()
    {
        return Ok(new MachineIdResult(PackCrypto.GetMachineId()));
    }

    /// <summary>
    /// Validate a pack JSON without importing it.
    /// </summary>
    [HttpPost("validate")]
    public ActionResult<PackValidationResult> Validate([FromBody] PackValidateRequest request)
    {
        try
        {
            var pack = PackCrypto.DeserializePack(request.PackJson);
            return Ok(new PackValidationResult(
                true, null, pack.PackId, pack.Name,
                pack.Memories.Count, pack.Preferences.Count, pack.AntiPatterns.Count));
        }
        catch (Exception ex)
        {
            return Ok(new PackValidationResult(false, ex.Message, "", "", 0, 0, 0));
        }
    }
}

public record PackImportRequest(string PackJson, string? DecryptionKey = null, string? TargetProject = null);
public record PackValidateRequest(string PackJson);
public record KeygenResult(string Key);
public record MachineIdResult(string MachineId);
public record PackValidationResult(
    bool IsValid, string? Error, string PackId, string PackName,
    int MemoryCount, int PreferenceCount, int AntiPatternCount);
