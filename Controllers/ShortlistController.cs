using Microsoft.AspNetCore.Mvc;
using Mogify.Api.Models;
using Mogify.Api.Services;

namespace Mogify.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ShortlistController : ControllerBase
{
    private readonly SupabaseService _supabase;
    private readonly ClaudeService _claude;

    public ShortlistController(SupabaseService supabase, ClaudeService claude)
    {
        _supabase = supabase;
        _claude = claude;
    }

    // ── POST /shortlist/generate ───────────────────────────────────────────────
    // Instant algorithmic ranking based on grades, location, school type.
    // No AI involved — returns in ~200ms.
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateShortlistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Profile.TargetSubject))
            return BadRequest(new { error = "target_subject is required." });

        // Fetch universities and courses in parallel
        var universitiesTask = _supabase.GetUniversitiesAsync();
        var coursesTask = _supabase.GetCoursesForSubjectAsync(request.Profile.TargetSubject);
        await Task.WhenAll(universitiesTask, coursesTask);
        var universities = universitiesTask.Result;
        var courses = coursesTask.Result;

        var coursesBySlug = courses.ToDictionary(c => c.UniversitySlug);

        if (coursesBySlug.Count == 0)
            return NotFound(new { error = $"No universities found for subject: {request.Profile.TargetSubject}" });

        var studentGradeScore = GradeScore(request.Profile.PredictedGrades);
        var isStateSchool = request.Profile.SchoolType?.Contains("state", StringComparison.OrdinalIgnoreCase) == true
                         || request.Profile.SchoolType?.Contains("comprehensive", StringComparison.OrdinalIgnoreCase) == true
                         || request.Profile.SchoolType?.Contains("grammar", StringComparison.OrdinalIgnoreCase) == true;

        var shortlist = universities
            .Where(u => coursesBySlug.ContainsKey(u.Slug))
            .Select(u =>
            {
                var course = coursesBySlug[u.Slug];
                var offerScore = GradeScore(course.TypicalOffer);
                var diff = studentGradeScore - offerScore;

                var recommendationType = diff switch
                {
                    >= 2  => "safety",
                    >= 0  => "match",
                    >= -2 => "reach",
                    _     => "stretch"
                };

                var gradeAlignment = diff switch
                {
                    >= 3  => 60,
                    2     => 55,
                    1     => 50,
                    0     => 45,
                    -1    => 30,
                    -2    => 15,
                    _     => 5
                };

                var locationBonus    = MatchesLocation(u.Location, request.Profile.LocationPreference) ? 10 : 0;
                var contextualBonus  = (isStateSchool && u.ContextualAdmissions == true) ? 8 : 0;
                var acceptanceBonus  = u.AcceptanceRate.HasValue ? (int)(u.AcceptanceRate.Value / 10) : 5;
                var fitScore         = Math.Min(98, gradeAlignment + locationBonus + contextualBonus + acceptanceBonus);

                var gradeNote = diff switch
                {
                    >= 2  => $"your predicted {request.Profile.PredictedGrades} comfortably exceeds their typical offer of {course.TypicalOffer}",
                    >= 0  => $"your predicted {request.Profile.PredictedGrades} matches their typical offer of {course.TypicalOffer}",
                    >= -1 => $"their typical offer of {course.TypicalOffer} is a slight stretch on your predicted {request.Profile.PredictedGrades}",
                    _     => $"their typical offer of {course.TypicalOffer} is a significant stretch on your predicted {request.Profile.PredictedGrades}"
                };
                var contextNote  = (isStateSchool && u.ContextualAdmissions == true) ? " They offer contextual admissions, which works in your favour." : "";
                var lookingFor   = !string.IsNullOrWhiteSpace(u.WhatTheyLookFor) ? $" They value: {u.WhatTheyLookFor}." : "";
                var reasoning    = $"{u.Name} is a {recommendationType} — {gradeNote}.{lookingFor}{contextNote}";

                return new ShortlistItem(u.Slug, u.Name, u.Location, fitScore, recommendationType, reasoning, course.TypicalOffer, course.AdmissionsTest);
            })
            .Where(r => r.RecommendationType != "stretch")
            .OrderByDescending(r => r.FitScore)
            .Take(8)
            .ToList();

        return Ok(new { shortlist, generated_at = DateTime.UtcNow, ai_enhanced = false });
    }

    // ── POST /shortlist/enhance ────────────────────────────────────────────────
    // Called only when the user has filled in career goals or interests.
    // Claude reads their goals and rewrites the reasoning for each university
    // to explain specifically why it fits their ambitions.
    [HttpPost("enhance")]
    public async Task<IActionResult> Enhance([FromBody] EnhanceShortlistRequest request)
    {
        if (request.Shortlist == null || request.Shortlist.Count == 0)
            return BadRequest(new { error = "shortlist is required." });

        if (string.IsNullOrWhiteSpace(request.CareerGoals) && string.IsNullOrWhiteSpace(request.Interests))
            return BadRequest(new { error = "career_goals or interests required for AI enhancement." });

        var enhanced = await _claude.EnhanceShortlistAsync(
            request.Shortlist,
            request.CareerGoals ?? "",
            request.Interests ?? "",
            request.Profile);

        return Ok(new { shortlist = enhanced, generated_at = DateTime.UtcNow, ai_enhanced = true });
    }

    private static int GradeScore(string? grades)
    {
        if (string.IsNullOrWhiteSpace(grades)) return 0;
        return grades.Trim().ToUpper() switch
        {
            "A*A*A*" => 9,
            "A*A*A"  => 8,
            "A*AA"   => 7,
            "AAA"    => 6,
            "AAB"    => 5,
            "ABB"    => 4,
            "BBB"    => 3,
            "BBC"    => 2,
            "BCC"    => 1,
            _        => 0
        };
    }

    private static bool MatchesLocation(string? uniLocation, string? preference)
    {
        if (string.IsNullOrWhiteSpace(preference) || preference.Equals("no preference", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.IsNullOrWhiteSpace(uniLocation)) return false;
        return uniLocation.Contains(preference, StringComparison.OrdinalIgnoreCase)
            || preference.Contains(uniLocation, StringComparison.OrdinalIgnoreCase);
    }
}

public record GenerateShortlistRequest(StudentProfile Profile);

public record EnhanceShortlistRequest(
    List<ShortlistItem> Shortlist,
    string? CareerGoals,
    string? Interests,
    StudentProfile Profile);

public record ShortlistItem(
    string Slug,
    string Name,
    string? Location,
    int FitScore,
    string RecommendationType,
    string Reasoning,
    string? TypicalOffer,
    string? AdmissionsTest);
