using Microsoft.AspNetCore.Mvc;
using Mogify.Api.Models;
using Mogify.Api.Services;
using System.Text.Json;

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

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateShortlistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Profile.TargetSubject))
            return BadRequest(new { error = "target_subject is required." });

        // Fetch all universities and courses for the target subject
        var universities = await _supabase.GetUniversitiesAsync();
        var courses = await _supabase.GetCoursesForSubjectAsync(request.Profile.TargetSubject);

        var coursesBySlug = courses.ToDictionary(c => c.UniversitySlug);

        var universitiesData = universities
            .Where(u => coursesBySlug.ContainsKey(u.Slug))
            .Select(u => new
            {
                slug = u.Slug,
                name = u.Name,
                location = u.Location,
                type = u.Type,
                character = u.Character,
                what_they_look_for = u.WhatTheyLookFor,
                red_flags = u.RedFlags,
                acceptance_rate = u.AcceptanceRate,
                contextual_admissions = u.ContextualAdmissions,
                course = coursesBySlug.TryGetValue(u.Slug, out var course) ? new
                {
                    typical_offer = course.TypicalOffer,
                    entry_requirements = course.EntryRequirements,
                    admissions_test = course.AdmissionsTest,
                    interview_format = course.InterviewFormat,
                    interview_style = course.InterviewStyle,
                    ps_guidance = course.PsGuidance
                } : null
            })
            .Cast<object>()
            .ToList();

        if (universitiesData.Count == 0)
            return NotFound(new { error = $"No universities found for subject: {request.Profile.TargetSubject}" });

        var result = await _claude.GenerateShortlistAsync(request.Profile, universitiesData);

        // Try to parse as JSON; return raw string if parsing fails
        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(result);
            return Ok(new { shortlist = parsed, generated_at = DateTime.UtcNow });
        }
        catch
        {
            return Ok(new { shortlist = result, generated_at = DateTime.UtcNow });
        }
    }
}

public record GenerateShortlistRequest(StudentProfile Profile);
