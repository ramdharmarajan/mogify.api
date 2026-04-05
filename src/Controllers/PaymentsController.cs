using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mogify.Api.Services;
using System.Security.Claims;

namespace Mogify.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly StripeService _stripe;
    private readonly ILogger<PaymentsController> _logger;
    private readonly IConfiguration _config;

    public PaymentsController(StripeService stripe, ILogger<PaymentsController> logger, IConfiguration config)
    {
        _stripe = stripe;
        _logger = logger;
        _config = config;
    }

    [HttpPost("create-checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] CreateCheckoutRequest request)
    {
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (request.Tier != "applicant" && request.Tier != "premium")
            return BadRequest(new { error = "tier must be 'applicant' or 'premium'." });

        var frontendUrl = _config["CORS_ORIGINS"]?.Split(',').FirstOrDefault() ?? "https://mogify.co.uk";
        var session = await _stripe.CreateCheckoutSessionAsync(
            userId,
            request.Tier,
            $"{frontendUrl}/payment/success",
            $"{frontendUrl}/payment/cancel");

        return Ok(new { url = session.Url, session_id = session.Id });
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(signature))
            return BadRequest();

        try
        {
            var stripeEvent = _stripe.ConstructWebhookEvent(payload, signature);

            if (stripeEvent.Type == "checkout.session.completed")
            {
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                if (session?.Metadata?.TryGetValue("user_id", out var userId) == true &&
                    session.Metadata.TryGetValue("tier", out var tier) == true)
                {
                    _logger.LogInformation("Payment completed: user {UserId} upgraded to {Tier}", userId, tier);
                    // TODO: Update user tier in Supabase
                    // await _supabase.UpdateUserTierAsync(userId, tier);
                }
            }

            return Ok();
        }
        catch (Stripe.StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook validation failed.");
            return BadRequest(new { error = "Webhook validation failed." });
        }
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        // TODO: read tier from Supabase user metadata
        return Ok(new { tier = "free", valid_until = (DateTime?)null });
    }
}

public record CreateCheckoutRequest(string Tier);
