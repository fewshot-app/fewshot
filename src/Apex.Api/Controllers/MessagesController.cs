using Apex.Core.Interfaces;
using Apex.Core.Models;
using Apex.Core.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messages;
    private readonly IAuditService _audit;

    public MessagesController(IMessageService messages, IAuditService audit)
    {
        _messages = messages;
        _audit = audit;
    }

    [HttpPost]
    public async Task<ActionResult<Message>> Log([FromBody] LogMessageRequest req)
    {
        // Run audit pipeline before logging
        var auditResult = await _audit.AnalyzeAsync(req.Content, req.SessionId);
        if (!auditResult.IsSafe)
            return StatusCode(403, new { reason = "Content blocked by audit pipeline", findings = auditResult.Findings.Select(f => f.DetectedType) });

        var content = auditResult.RequiresReview ? auditResult.RedactedContent! : req.Content;
        var msg = await _messages.LogMessageAsync(req.SessionId, req.Role, content, req.TokenCount);
        return Ok(msg);
    }

    [HttpGet("session/{sessionId}")]
    public async Task<ActionResult<List<Message>>> GetBySession(int sessionId)
        => Ok(await _messages.GetSessionMessagesAsync(sessionId));
}

public record LogMessageRequest(int SessionId, MessageRole Role, string Content, int? TokenCount = null);
