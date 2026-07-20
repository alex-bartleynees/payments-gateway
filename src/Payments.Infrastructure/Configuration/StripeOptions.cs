namespace Payments.Infrastructure.Configuration;

/// <summary>
/// Global provider credentials for the single Stripe account the gateway operates. Per-product pricing,
/// trial length and redirect URLs live in the product registry (<c>Products</c> config section), not here.
/// </summary>
public class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = string.Empty;

    public string PublishableKey { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;
}
