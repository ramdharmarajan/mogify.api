using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mogify.Api.Models;
using Mogify.Api.Services;

namespace Mogify.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ScholarshipsController : ControllerBase
{
    private readonly SupabaseService _supabase;

    public ScholarshipsController(SupabaseService supabase)
    {
        _supabase = supabase;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? subject)
    {
        var scholarships = await _supabase.GetScholarshipsAsync();

        if (!string.IsNullOrWhiteSpace(subject))
            scholarships = scholarships
                .Where(s => s.Subjects?.Any(sub => sub.Equals(subject, StringComparison.OrdinalIgnoreCase)) == true)
                .ToList();

        return Ok(scholarships);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var scholarship = await _supabase.GetScholarshipAsync(id);
        if (scholarship == null)
            return NotFound(new { error = $"Scholarship {id} not found." });
        return Ok(scholarship);
    }
}
