using Stripe;
using Stripe.Checkout;

namespace Mogify.Api.Services;

public class StripeService
{
    private readonly string _webhookSecret;

    public StripeService(IConfiguration configuration)
    {
        StripeConfiguration.ApiKey = configuration["STRIPE_SECRET_KEY"]
            ?? throw new InvalidOperationException("STRIPE_SECRET_KEY is not configured.");
        _webhookSecret = configuration["STRIPE_WEBHOOK_SECRET"]
            ?? throw new InvalidOperationException("STRIPE_WEBHOOK_SECRET is not configured.");
    }

    public async Task<Session> CreateCheckoutSessionAsync(string userId, string tier, string successUrl, string cancelUrl)
    {
        var priceId = tier switch
        {
            "applicant" => "price_1TIxAQBNaSSijei9KV1wD0hS",
            "premium"   => "price_1TIxAtBNaSSijei9bMYIsmym",
            _ => throw new ArgumentException($"Unknown tier: {tier}")
        };

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = ["card"],
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1
                }
            ],
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string>
            {
                ["user_id"] = userId,
                ["tier"] = tier
            }
        };

        var service = new SessionService();
        return await service.CreateAsync(options);
    }

    public Event ConstructWebhookEvent(string payload, string signature)
    {
        return EventUtility.ConstructEvent(payload, signature, _webhookSecret);
    }
}
