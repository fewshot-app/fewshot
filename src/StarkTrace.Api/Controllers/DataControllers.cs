using StarkTrace.Core.Interfaces;
using StarkTrace.Core.Models;
using StarkTrace.Core.Enums;
using Microsoft.AspNetCore.Mvc;

namespace StarkTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuggestionsController : ControllerBase
{
    private readonly ISuggestionService _suggestions;

    public SuggestionsController(ISuggestionService suggestions) => _suggestions = suggestions;

    [HttpPost]
    public async Task<ActionResult<Suggestion>> Create([FromBody] Suggestion suggestion)
        => Ok(await _suggestions.CreateAsync(suggestion));

    [HttpGet("session/{sessionId}")]
    public async Task<ActionResult<List<Suggestion>>> GetBySession(int sessionId)
        => Ok(await _suggestions.GetBySessionAsync(sessionId));

    [HttpPost("{id}/apply")]
    public async Task<IActionResult> MarkApplied(int id)
    {
        await _suggestions.MarkAppliedAsync(id);
        return NoContent();
    }
}

[ApiController]
[Route("api/[controller]")]
public class OutcomesController : ControllerBase
{
    private readonly IOutcomeService _outcomes;

    public OutcomesController(IOutcomeService outcomes) => _outcomes = outcomes;

    [HttpPost]
    public async Task<ActionResult<Outcome>> Record([FromBody] Outcome outcome)
        => Ok(await _outcomes.RecordAsync(outcome));

    [HttpGet("suggestion/{suggestionId}")]
    public async Task<ActionResult<List<Outcome>>> GetBySuggestion(int suggestionId)
        => Ok(await _outcomes.GetBySuggestionAsync(suggestionId));
}

[ApiController]
[Route("api/[controller]")]
public class PreferencesController : ControllerBase
{
    private readonly IPreferenceService _preferences;

    public PreferencesController(IPreferenceService preferences) => _preferences = preferences;

    [HttpPost]
    public async Task<ActionResult<Preference>> Upsert([FromBody] Preference preference)
        => Ok(await _preferences.UpsertAsync(preference));

    [HttpPut("{id}")]
    public async Task<ActionResult<Preference>> Update(int id, [FromBody] UpdatePreferenceRequest req)
    {
        var existing = (await _preferences.GetAllAsync()).FirstOrDefault(p => p.PrefId == id);
        if (existing == null) return NotFound();
        existing.Value = req.Value;
        existing.ConfidenceScore = req.ConfidenceScore;
        existing.IsExplicit = req.IsExplicit;
        return Ok(await _preferences.UpsertAsync(existing));
    }

    [HttpGet]
    public async Task<ActionResult<List<Preference>>> GetAll()
        => Ok(await _preferences.GetAllAsync());

    [HttpPost("{id}/reinforce")]
    public async Task<IActionResult> Reinforce(int id)
    {
        await _preferences.ReinforceAsync(id);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _preferences.DeleteAsync(id);
        return NoContent();
    }
}

[ApiController]
[Route("api/[controller]")]
public class AntiPatternsController : ControllerBase
{
    private readonly IAntiPatternService _antiPatterns;

    public AntiPatternsController(IAntiPatternService antiPatterns) => _antiPatterns = antiPatterns;

    [HttpPost]
    public async Task<ActionResult<AntiPattern>> Create([FromBody] AntiPattern antiPattern)
        => Ok(await _antiPatterns.CreateAsync(antiPattern));

    [HttpGet]
    public async Task<ActionResult<List<AntiPattern>>> GetAll([FromQuery] string? language = null)
        => Ok(language != null
            ? await _antiPatterns.GetByLanguageAsync(language)
            : await _antiPatterns.GetAllAsync());

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _antiPatterns.DeleteAsync(id);
        return NoContent();
    }
}

public record UpdatePreferenceRequest(string Value, double ConfidenceScore, bool IsExplicit);
