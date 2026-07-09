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

    // Trimmed projection — strong_approach / common_mistakes are the paid
    // product's model answers and must not reach the browser during practice.
    private static object ToClientQuestion(InterviewQuestion q) =>
        new { q.Id, q.Question, q.Type, q.Difficulty, q.WhatIsBeingTested };

    [HttpGet("questions/{universitySlug}/{subject}")]
    public async Task<IActionResult> GetQuestions(string universitySlug, string subject)
    {
        var questions = await _supabase.GetInterviewQuestionsAsync(universitySlug, subject);
        return Ok(questions.Select(ToClientQuestion));
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

        // The client drives the question sequence and answers with question_id;
        // turns are recorded as answers arrive.
        return CreatedAtAction(nameof(GetSession), new { id = session.Id }, new
        {
            session.Id,
            session.CreatedAt,
            interview_format = course?.InterviewFormat ?? "Panel",
            questions = questions.Select(ToClientQuestion)
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

        if (string.IsNullOrWhiteSpace(request.Answer))
            return BadRequest(new { error = "answer is required." });

        var questions = await _supabase.GetInterviewQuestionsAsync(session.UniversitySlug, session.Subject);
        var course = await _supabase.GetCourseAsync(session.UniversitySlug, session.Subject);

        // The client says which question it asked; grade the answer against that
        // question, not a server-picked one. Fall back to the legacy pending-turn
        // flow for sessions created before this change.
        string questionText;
        var legacyPendingTurn = session.Turns.LastOrDefault(t => t.Answer == null);
        if (!string.IsNullOrWhiteSpace(request.QuestionId))
        {
            var question = questions.FirstOrDefault(q => q.Id == request.QuestionId);
            if (question == null)
                return BadRequest(new { error = "question_id not found for this session's university and subject." });
            questionText = question.Question;
        }
        else if (legacyPendingTurn != null)
        {
            questionText = legacyPendingTurn.Question;
        }
        else
        {
            return BadRequest(new { error = "question_id is required." });
        }

        var feedback = await _claude.GetInterviewFeedbackAsync(
            session.UniversitySlug, session.Subject,
            course?.InterviewFormat ?? "Panel",
            questions, questionText, request.Answer);
        var score = ExtractScore(feedback);

        if (!string.IsNullOrWhiteSpace(request.QuestionId))
        {
            session.Turns.Add(new InterviewTurn
            {
                Question = questionText,
                Answer = request.Answer,
                Feedback = feedback,
                Score = score
            });
        }
        else
        {
            legacyPendingTurn!.Answer = request.Answer;
            legacyPendingTurn.Feedback = feedback;
            legacyPendingTurn.Score = score;
        }

        await _supabase.SaveInterviewTurnsAsync(id, session.Turns);

        return Ok(new { feedback, score });
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
        var answered = session.Turns.Where(t => t.Answer != null).ToList();
        var totalScore = answered.Sum(t => t.Score ?? 0);

        return Ok(new
        {
            total_score = totalScore,
            feedback = summary.Feedback,
            strengths = summary.Strengths,
            improvements = summary.Improvements,
            questions_answered = answered.Count
        });
    }

    private static int? ExtractScore(string feedback)
    {
        var match = System.Text.RegularExpressions.Regex.Match(feedback, @"\b(\d{1,2})/10\b");
        return match.Success && int.TryParse(match.Groups[1].Value, out var s) ? s : null;
    }
}

public record CreateInterviewSessionRequest(string UniversitySlug, string Subject);
public record SubmitAnswerRequest(string Answer, string? QuestionId);
