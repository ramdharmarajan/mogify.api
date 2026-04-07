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

                var locationBonus    = MatchesLocation(u.Location, request.Profile.LocationPreference) ? 20 : 0;
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

    // Maps city/county names (as stored in DB) → frontend dropdown region labels.
    // Covers cities, counties, and common DB variants (e.g. "West Yorkshire", "Tyne and Wear").
    private static readonly Dictionary<string, string> _cityToRegion = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Scotland ──────────────────────────────────────────────────────────
        ["Edinburgh"]       = "Scotland",
        ["Glasgow"]         = "Scotland",
        ["Aberdeen"]        = "Scotland",
        ["St Andrews"]      = "Scotland",
        ["Dundee"]          = "Scotland",
        ["Stirling"]        = "Scotland",
        ["Inverness"]       = "Scotland",
        ["Perth"]           = "Scotland",
        ["Strathclyde"]     = "Scotland",
        ["Heriot-Watt"]     = "Scotland",

        // ── London ────────────────────────────────────────────────────────────
        ["London"]          = "London",
        ["Egham"]           = "London",   // Royal Holloway
        ["Uxbridge"]        = "London",   // Brunel

        // ── South East ────────────────────────────────────────────────────────
        ["Oxford"]          = "South East",
        ["Cambridge"]       = "South East",
        ["Brighton"]        = "South East",
        ["Hove"]            = "South East",
        ["Southampton"]     = "South East",
        ["Portsmouth"]      = "South East",
        ["Reading"]         = "South East",
        ["Canterbury"]      = "South East",
        ["Guildford"]       = "South East",
        ["Surrey"]          = "South East",
        ["Kent"]            = "South East",
        ["Sussex"]          = "South East",
        ["Hampshire"]       = "South East",
        ["Chichester"]      = "South East",
        ["Winchester"]      = "South East",
        ["Buckinghamshire"] = "South East",
        ["Milton Keynes"]   = "South East",
        ["Hertfordshire"]   = "South East",
        ["Hatfield"]        = "South East",
        ["Essex"]           = "South East",
        ["Colchester"]      = "South East",
        ["Chelmsford"]      = "South East",
        ["Norwich"]         = "South East",
        ["Norfolk"]         = "South East",
        // South West — no dropdown option, map to South East as nearest
        ["Bristol"]         = "South East",
        ["Bath"]            = "South East",
        ["Exeter"]          = "South East",
        ["Plymouth"]        = "South East",
        ["Falmouth"]        = "South East",
        ["Cornwall"]        = "South East",
        ["Devon"]           = "South East",
        ["Somerset"]        = "South East",
        ["Gloucestershire"] = "South East",
        ["Cheltenham"]      = "South East",
        ["Bournemouth"]     = "South East",
        ["Dorset"]          = "South East",

        // ── Midlands ──────────────────────────────────────────────────────────
        ["Birmingham"]      = "Midlands",
        ["Nottingham"]      = "Midlands",
        ["Leicester"]       = "Midlands",
        ["Coventry"]        = "Midlands",
        ["Warwick"]         = "Midlands",
        ["Derby"]           = "Midlands",
        ["Lincoln"]         = "Midlands",
        ["Keele"]           = "Midlands",
        ["Staffordshire"]   = "Midlands",
        ["Stoke"]           = "Midlands",
        ["Worcester"]       = "Midlands",
        ["Wolverhampton"]   = "Midlands",
        ["Northampton"]     = "Midlands",
        ["West Midlands"]   = "Midlands",
        ["East Midlands"]   = "Midlands",
        ["Loughborough"]    = "Midlands",
        ["Aston"]           = "Midlands",
        ["De Montfort"]     = "Midlands",

        // ── North ─────────────────────────────────────────────────────────────
        ["Manchester"]      = "North",
        ["Leeds"]           = "North",
        ["Sheffield"]       = "North",
        ["Liverpool"]       = "North",
        ["Newcastle"]       = "North",
        ["York"]            = "North",
        ["Durham"]          = "North",
        ["Lancaster"]       = "North",
        ["Hull"]            = "North",
        ["Sunderland"]      = "North",
        ["Middlesbrough"]   = "North",
        ["Teesside"]        = "North",
        ["Chester"]         = "North",
        ["Huddersfield"]    = "North",
        ["Bradford"]        = "North",
        ["Salford"]         = "North",
        ["Bolton"]          = "North",
        ["Preston"]         = "North",
        ["Northumbria"]     = "North",
        ["Carlisle"]        = "North",
        ["Cumbria"]         = "North",
        ["Yorkshire"]       = "North",
        ["West Yorkshire"]  = "North",
        ["South Yorkshire"] = "North",
        ["North Yorkshire"] = "North",
        ["East Yorkshire"]  = "North",
        ["Lancashire"]      = "North",
        ["Merseyside"]      = "North",
        ["Tyne and Wear"]   = "North",
        ["Tyne"]            = "North",
        ["Wear"]            = "North",
        ["Cheshire"]        = "North",
        ["Humberside"]      = "North",
    };

    private static bool MatchesLocation(string? uniLocation, string? preference)
    {
        if (string.IsNullOrWhiteSpace(preference) || preference.Equals("no preference", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.IsNullOrWhiteSpace(uniLocation)) return false;

        // Direct match (e.g. DB stores "London" and preference is "London")
        if (uniLocation.Contains(preference, StringComparison.OrdinalIgnoreCase))
            return true;

        // Map city → region and compare (e.g. DB stores "Edinburgh", preference is "Scotland")
        var city = _cityToRegion.Keys.FirstOrDefault(c => uniLocation.Contains(c, StringComparison.OrdinalIgnoreCase));
        if (city != null && _cityToRegion.TryGetValue(city, out var region))
            return region.Equals(preference, StringComparison.OrdinalIgnoreCase);

        return false;
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
