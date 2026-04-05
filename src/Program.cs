using Anthropic.SDK;
using Mogify.Api.Middleware;
using Mogify.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ──────────────────────────────────────────────────────────
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// ── CORS ───────────────────────────────────────────────────────────────────
var allowedOrigins = (builder.Configuration["CORS_ORIGINS"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ── Supabase ───────────────────────────────────────────────────────────────
var supabaseUrl = builder.Configuration["SUPABASE_URL"]
    ?? throw new InvalidOperationException("SUPABASE_URL is not configured.");
var supabaseKey = builder.Configuration["SUPABASE_SERVICE_KEY"]
    ?? throw new InvalidOperationException("SUPABASE_SERVICE_KEY is not configured.");

builder.Services.AddSingleton(_ => new Supabase.Client(
    supabaseUrl,
    supabaseKey,
    new Supabase.SupabaseOptions { AutoConnectRealtime = false }));
builder.Services.AddSingleton<SupabaseService>();

// ── Anthropic / Claude ─────────────────────────────────────────────────────
var anthropicKey = builder.Configuration["ANTHROPIC_API_KEY"]
    ?? throw new InvalidOperationException("ANTHROPIC_API_KEY is not configured.");
builder.Services.AddSingleton(_ => new AnthropicClient(anthropicKey));
builder.Services.AddSingleton<ClaudeService>();

// ── Stripe ─────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<StripeService>();

// ── Session store (in-memory for MVP) ──────────────────────────────────────
builder.Services.AddSingleton<SessionStore>();

// ── MVC ────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();

var app = builder.Build();

// ── Initialise Supabase client ─────────────────────────────────────────────
var supabase = app.Services.GetRequiredService<Supabase.Client>();
await supabase.InitializeAsync();

// ── Middleware pipeline ────────────────────────────────────────────────────
app.UseCors();
app.UseMiddleware<SupabaseAuthMiddleware>();
app.MapControllers();

// ── Health check ───────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .AllowAnonymous();

// ── Railway port binding ───────────────────────────────────────────────────
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");

app.Run();
