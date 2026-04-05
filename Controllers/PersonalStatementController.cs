using Microsoft.AspNetCore.Mvc;
using Mogify.Api.Models;
using Mogify.Api.Services;
using System.Security.Claims;

namespace Mogify.Api.Controllers;

[ApiController]
[Route("ps")]
public class PersonalStatementController : ControllerBase
{
    private readonly ClaudeService _claude;
    private readonly SessionStore _sessions;

    public PersonalStatementController(ClaudeService claude, SessionStore sessions)
    {
        _claude = claude;
        _sessions = sessions;
    }

    private string GetUserId() =>
        User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

    [HttpGet("sessions")]
    public IActionResult GetSessions()
    {
        var userId = GetUserId();
        var sessions = _sessions.GetPsSessionsForUser(userId);
        return Ok(sessions.Select(s => new
        {
            s.Id,
            s.CreatedAt,
            message_count = s.Messages.Count,
            last_message = s.Messages.LastOrDefault()?.Content?[..Math.Min(100, s.Messages.LastOrDefault()?.Content.Length ?? 0)]
        }));
    }

    [HttpPost("sessions")]
    public IActionResult CreateSession()
    {
        var userId = GetUserId();
        var session = _sessions.CreatePsSession(userId);
        return CreatedAtAction(nameof(GetSession), new { id = session.Id }, new { session.Id, session.CreatedAt });
    }

    [HttpGet("sessions/{id}")]
    public IActionResult GetSession(string id)
    {
        var session = _sessions.GetPsSession(id);
        if (session == null)
            return NotFound(new { error = "Session not found." });

        var userId = GetUserId();
        if (session.UserId != userId)
            return Forbid();

        return Ok(session);
    }

    [HttpPost("sessions/{id}/message")]
    public async Task<IActionResult> SendMessage(string id, [FromBody] PsMessageRequest request)
    {
        var session = _sessions.GetPsSession(id);
        if (session == null)
            return NotFound(new { error = "Session not found." });

        var userId = GetUserId();
        if (session.UserId != userId)
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message cannot be empty." });

        // Add user message
        var userMsg = new PsMessage { Role = "user", Content = request.Message };
        _sessions.AddPsMessage(id, userMsg);

        // Get Claude response
        var profile = request.Profile ?? new StudentProfile();
        var targetUniversities = request.TargetUniversities ?? [];
        var assistantText = await _claude.SendPsCoachMessageAsync(profile, targetUniversities, session.Messages, request.Message);

        // Add assistant response
        var assistantMsg = new PsMessage { Role = "assistant", Content = assistantText };
        _sessions.AddPsMessage(id, assistantMsg);

        return Ok(new { response = assistantText });
    }

    [HttpGet("draft")]
    public IActionResult GetDraft()
    {
        // In MVP, draft is stored in memory or client-side. Expand to Supabase later.
        return Ok(new { draft = (string?)null, message = "No draft saved yet." });
    }

    [HttpPost("draft/generate")]
    public async Task<IActionResult> GenerateDraft([FromBody] GenerateDraftRequest request)
    {
        var userId = GetUserId();
        var sessions = _sessions.GetPsSessionsForUser(userId);

        if (sessions.Count == 0)
            return BadRequest(new { error = "No coaching sessions found. Start a session first." });

        var draft = await _claude.GeneratePsDraftAsync(request.Profile ?? new StudentProfile(), sessions);
        return Ok(new { draft, generated_at = DateTime.UtcNow });
    }

    [HttpPut("draft")]
    public IActionResult SaveDraft([FromBody] SaveDraftRequest request)
    {
        // Placeholder — persist to Supabase student_applications in V2
        return Ok(new { message = "Draft saved.", saved_at = DateTime.UtcNow });
    }

    [HttpGet("draft/feedback")]
    public async Task<IActionResult> GetFeedback([FromQuery] string draft)
    {
        if (string.IsNullOrWhiteSpace(draft))
            return BadRequest(new { error = "draft query parameter is required." });

        var feedback = await _claude.GetPsFeedbackAsync(draft);
        return Ok(new { feedback });
    }
}

public record PsMessageRequest(string Message, StudentProfile? Profile, List<string>? TargetUniversities);
public record GenerateDraftRequest(StudentProfile? Profile);
public record SaveDraftRequest(string Draft);
