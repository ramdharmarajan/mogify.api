using Microsoft.AspNetCore.Mvc;
using Mogify.Api.Models;
using Mogify.Api.Services;
using System.Security.Claims;

namespace Mogify.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class InterviewController : ControllerBase
{
    private readonly SupabaseService _supabase;
    private readonly ClaudeService _claude;

    public InterviewController(SupabaseService supabase, ClaudeService claude)
    {
        _supabase = supabase;
        _claude = claude;
    }

    private string GetUserId() =>
        User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "anonymous";

    [HttpGet("questions/{universitySlug}/{subject}")]
    public async Task<IActionResult> GetQuestions(string universitySlug, string subject)
    {
        var questions = await _supabase.GetInterviewQuestionsAsync(universitySlug, subject);
        return Ok(questions);
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] CreateInterviewSessionRequest request)
    {
        var userId = GetUserId();

        var questions = await _supabase.GetInterviewQuestionsAsync(request.UniversitySlug, request.Subject);
        if (questions.Count == 0)
            return NotFound(new { error = $"No questions found for {request.UniversitySlug} / {request.Subject}" });

        var session = await _supabase.CreateInterviewSessionAsync(userId, request.UniversitySlug, request.Subject);
        var course = await _supabase.GetCourseAsync(request.UniversitySlug, request.Subject);

        var rng = new Random();
        var firstQuestion = questions[rng.Next(questions.Count)];
        session.Turns.Add(new InterviewTurn { Question = firstQuestion.Question });
        await _supabase.SaveInterviewTurnsAsync(session.Id, session.Turns);

        return CreatedAtAction(nameof(GetSession), new { id = session.Id }, new
        {
            session.Id,
            session.CreatedAt,
            interview_format = course?.InterviewFormat ?? "Panel",
            first_question = firstQuestion.Question,
            question_type = firstQuestion.Type
        });
    }

    [HttpGet("sessions/{id}")]
    public async Task<IActionResult> GetSession(string id)
    {
        var session = await _supabase.GetInterviewSessionAsync(id);
        if (session == null)
            return NotFound(new { error = "Session not found." });

        if (session.UserId != GetUserId())
            return Forbid();

        return Ok(session);
    }

    [HttpPost("sessions/{id}/answer")]
    public async Task<IActionResult> SubmitAnswer(string id, [FromBody] SubmitAnswerRequest request)
    {
        var session = await _supabase.GetInterviewSessionAsync(id);
        if (session == null)
            return NotFound(new { error = "Session not found." });

        if (session.UserId != GetUserId())
            return Forbid();

        var currentTurn = session.Turns.LastOrDefault(t => t.Answer == null);
        if (currentTurn == null)
            return BadRequest(new { error = "No pending question in this session." });

        var questions = await _supabase.GetInterviewQuestionsAsync(session.UniversitySlug, session.Subject);
        var course = await _supabase.GetCourseAsync(session.UniversitySlug, session.Subject);

        var feedback = await _claude.GetInterviewFeedbackAsync(
            session.UniversitySlug, session.Subject,
            course?.InterviewFormat ?? "Panel",
            questions, currentTurn.Question, request.Answer);

        currentTurn.Answer = request.Answer;
        currentTurn.Feedback = feedback;
        currentTurn.Score = ExtractScore(feedback);

        string? nextQuestion = null;
        var answered = session.Turns.Where(t => t.Answer != null).Select(t => t.Question).ToHashSet();
        var unanswered = questions.Where(q => !answered.Contains(q.Question)).ToList();

        if (unanswered.Count > 0 && session.Turns.Count < 5)
        {
            var next = unanswered[new Random().Next(unanswered.Count)];
            session.Turns.Add(new InterviewTurn { Question = next.Question });
            nextQuestion = next.Question;
        }

        await _supabase.SaveInterviewTurnsAsync(id, session.Turns);

        return Ok(new { feedback, score = currentTurn.Score, next_question = nextQuestion });
    }

    [HttpGet("sessions/{id}/summary")]
    public async Task<IActionResult> GetSummary(string id)
    {
        var session = await _supabase.GetInterviewSessionAsync(id);
        if (session == null)
            return NotFound(new { error = "Session not found." });

        if (session.UserId != GetUserId())
            return Forbid();

        var summary = await _claude.GetInterviewSessionSummaryAsync(session);
        var avgScore = session.Turns
            .Where(t => t.Score.HasValue)
            .Select(t => t.Score!.Value)
            .DefaultIfEmpty(0)
            .Average();

        return Ok(new { summary, average_score = Math.Round(avgScore, 1), questions_answered = session.Turns.Count(t => t.Answer != null) });
    }

    private static int? ExtractScore(string feedback)
    {
        var match = System.Text.RegularExpressions.Regex.Match(feedback, @"\b(\d{1,2})/10\b");
        return match.Success && int.TryParse(match.Groups[1].Value, out var s) ? s : null;
    }
}

public record CreateInterviewSessionRequest(string UniversitySlug, string Subject);
public record SubmitAnswerRequest(string Answer);
