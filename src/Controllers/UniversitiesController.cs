using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mogify.Api.Services;

namespace Mogify.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class UniversitiesController : ControllerBase
{
    private readonly SupabaseService _supabase;

    public UniversitiesController(SupabaseService supabase)
    {
        _supabase = supabase;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll()
    {
        var universities = await _supabase.GetUniversitiesAsync();
        return Ok(universities);
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var university = await _supabase.GetUniversityAsync(slug);
        if (university == null)
            return NotFound(new { error = $"University '{slug}' not found." });
        return Ok(university);
    }
}
