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
        try
        {
            var universities = await _supabase.GetUniversitiesAsync();
            return Ok(universities);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
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
