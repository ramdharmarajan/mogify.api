using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Mogify.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    private readonly HttpClient _http;
    private readonly string _supabaseUrl;
    private readonly string _anonKey;

    public AuthController(IConfiguration config, IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient();
        _supabaseUrl = config["SUPABASE_URL"]!;
        _anonKey = config["SUPABASE_ANON_KEY"]!;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] AuthRequest request)
    {
        var response = await SupabaseAuthAsync("/auth/v1/signup", request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, JsonSerializer.Deserialize<object>(body));

        return Ok(JsonSerializer.Deserialize<object>(body));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] AuthRequest request)
    {
        var response = await SupabaseAuthAsync("/auth/v1/token?grant_type=password", request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, JsonSerializer.Deserialize<object>(body));

        return Ok(JsonSerializer.Deserialize<object>(body));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var payload = JsonSerializer.Serialize(new { refresh_token = request.RefreshToken });
        var response = await SupabaseAuthAsync("/auth/v1/token?grant_type=refresh_token",
            payload: payload);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, JsonSerializer.Deserialize<object>(body));

        return Ok(JsonSerializer.Deserialize<object>(body));
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue("sub");
        var email = User.FindFirstValue("email");
        return Ok(new { id = userId, email });
    }

    private async Task<HttpResponseMessage> SupabaseAuthAsync(string path, object? body = null, string? payload = null)
    {
        var url = _supabaseUrl.TrimEnd('/') + path;
        var json = payload ?? JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        content.Headers.Add("apikey", _anonKey);

        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        req.Headers.Add("apikey", _anonKey);

        return await _http.SendAsync(req);
    }
}

public record AuthRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
