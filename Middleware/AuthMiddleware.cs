using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Mogify.Api.Middleware;

public class SupabaseAuthMiddleware
{
    private static readonly HashSet<string> _publicPaths =
    [
        "/health",
        "/auth/register",
        "/auth/login",
        "/auth/refresh",
    ];

    private static readonly HashSet<string> _publicPrefixes =
    [
        "/universities",
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<SupabaseAuthMiddleware> _logger;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _oidcConfig;
    private readonly string _supabaseUrl;

    public SupabaseAuthMiddleware(RequestDelegate next, ILogger<SupabaseAuthMiddleware> logger, IConfiguration config)
    {
        _next = next;
        _logger = logger;
        _supabaseUrl = config["SUPABASE_URL"]!.TrimEnd('/');

        var metadataAddress = $"{_supabaseUrl}/auth/v1/.well-known/openid-configuration";
        _oidcConfig = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        var isPublic = _publicPaths.Contains(path) || _publicPrefixes.Any(p => path.StartsWith(p));

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
            var principal = await ValidateTokenAsync(token);

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

    private async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        try
        {
            var oidc = await _oidcConfig.GetConfigurationAsync(CancellationToken.None);

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = oidc.SigningKeys,
                ValidateIssuer = true,
                ValidIssuer = $"{_supabaseUrl}/auth/v1",
                ValidateAudience = true,
                ValidAudience = "authenticated",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var handler = new JwtSecurityTokenHandler();
            handler.InboundClaimTypeMap.Clear();
            return handler.ValidateToken(token, validationParams, out _);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed.");
            return null;
        }
    }
}
