using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> Register([FromBody] AuthRequest request)
    {
        return await ProxyToSupabase("/auth/v1/signup",
            JsonSerializer.Serialize(new { email = request.Email, password = request.Password }));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthRequest request)
    {
        return await ProxyToSupabase("/auth/v1/token?grant_type=password",
            JsonSerializer.Serialize(new { email = request.Email, password = request.Password }));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        return await ProxyToSupabase("/auth/v1/token?grant_type=refresh_token",
            JsonSerializer.Serialize(new { refresh_token = request.RefreshToken }));
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue("sub");
        var email = User.FindFirstValue("email");
        return Ok(new { id = userId, email });
    }

    private async Task<IActionResult> ProxyToSupabase(string path, string json)
    {
        var url = _supabaseUrl.TrimEnd('/') + path;
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("apikey", _anonKey);

        var response = await _http.SendAsync(req);
        var body = await response.Content.ReadAsStringAsync();

        return Content(body, "application/json", Encoding.UTF8);
    }
}

public record AuthRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
