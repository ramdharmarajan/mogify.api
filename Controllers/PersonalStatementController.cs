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
    private readonly SupabaseService _supabase;

    public PersonalStatementController(ClaudeService claude, SupabaseService supabase)
    {
        _claude = claude;
        _supabase = supabase;
    }

    private string GetUserId() =>
        User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "anonymous";

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var userId = GetUserId();
        var sessions = await _supabase.GetPsSessionsForUserAsync(userId);
        return Ok(sessions.Select(s => new
        {
            s.Id,
            s.CreatedAt,
            messages = s.Messages
        }));
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession()
    {
        var userId = GetUserId();
        var session = await _supabase.CreatePsSessionAsync(userId);
        return CreatedAtAction(nameof(GetSession), new { id = session.Id },
            new { session.Id, session.CreatedAt, messages = session.Messages });
    }

    [HttpGet("sessions/{id}")]
    public async Task<IActionResult> GetSession(string id)
    {
        var session = await _supabase.GetPsSessionAsync(id);
        if (session == null)
            return NotFound(new { error = "Session not found." });

        if (session.UserId != GetUserId())
            return Forbid();

        return Ok(session);
    }

    [HttpPost("sessions/{id}/message")]
    public async Task<IActionResult> SendMessage(string id, [FromBody] PsMessageRequest request)
    {
        var session = await _supabase.GetPsSessionAsync(id);
        if (session == null)
            return NotFound(new { error = "Session not found." });

        if (session.UserId != GetUserId())
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message cannot be empty." });

        var profile = request.Profile ?? new StudentProfile();
        var targetUniversities = request.TargetUniversities ?? [];
        var assistantText = await _claude.SendPsCoachMessageAsync(profile, targetUniversities, session.Messages, request.Message);

        var assistantMessage = new PsMessage { Role = "assistant", Content = assistantText };
        session.Messages.Add(new PsMessage { Role = "user", Content = request.Message });
        session.Messages.Add(assistantMessage);
        await _supabase.SavePsSessionMessagesAsync(id, session.Messages);

        return Ok(assistantMessage);
    }

    [HttpPost("draft/generate")]
    public async Task<IActionResult> GenerateDraft([FromBody] GenerateDraftRequest request)
    {
        var userId = GetUserId();
        var sessions = await _supabase.GetPsSessionsForUserAsync(userId);

        if (sessions.Count == 0)
            return BadRequest(new { error = "No coaching sessions found. Start a session first." });

        var draft = await _claude.GeneratePsDraftAsync(request.Profile ?? new StudentProfile(), sessions);
        return Ok(new { draft, generated_at = DateTime.UtcNow });
    }

    // POST body rather than query string — drafts run to 4,000 characters,
    // which risks blowing the request-line length limit as a URL.
    [HttpPost("draft/feedback")]
    public async Task<IActionResult> GetFeedback([FromBody] DraftFeedbackRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Draft))
            return BadRequest(new { error = "draft is required." });

        var feedback = await _claude.GetPsFeedbackAsync(request.Draft);
        return Ok(new { feedback });
    }
}

public record PsMessageRequest(string Message, StudentProfile? Profile, List<string>? TargetUniversities);
public record GenerateDraftRequest(StudentProfile? Profile);
public record DraftFeedbackRequest(string Draft);
