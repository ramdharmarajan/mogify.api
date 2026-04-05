using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Mogify.Api.Middleware;

public class SupabaseAuthMiddleware
{
    private static readonly HashSet<string> _publicPrefixes =
    [
        "/health",
        "/auth/",
        "/universities",
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<SupabaseAuthMiddleware> _logger;

    public SupabaseAuthMiddleware(RequestDelegate next, ILogger<SupabaseAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        var isPublic = _publicPrefixes.Any(p => path.StartsWith(p));

        if (!isPublic)
        {
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (authHeader == null || !authHeader.StartsWith("Bearer "))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid Authorization header." });
                return;
            }

            var token = authHeader["Bearer ".Length..].Trim();
            var principal = ValidateToken(token);

            if (principal == null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid or expired token." });
                return;
            }

            context.User = principal;
        }

        await _next(context);
    }

    private ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            // Supabase JWTs are signed with the JWT secret (from project settings)
            // For now we decode without verification for development; lock down before launch
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
                return null;

            var jwt = handler.ReadJwtToken(token);

            var claims = jwt.Claims.ToList();
            var identity = new ClaimsIdentity(claims, "Supabase");
            return new ClaimsPrincipal(identity);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed.");
            return null;
        }
    }
}
