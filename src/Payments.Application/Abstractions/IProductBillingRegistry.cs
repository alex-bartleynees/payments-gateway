namespace Payments.Application.Abstractions;

/// <summary>
/// Per-product billing configuration (the recurring Price, trial length and the redirect URLs). One
/// entry per product the gateway bills for; the single provider secret + webhook secret stay global.
/// </summary>
public class ProductBillingConfig
{
    /// <summary>The recurring Price the Checkout Session subscribes the customer to.</summary>
    public string PriceId { get; set; } = string.Empty;

    /// <summary>Trial length applied per Checkout Session (0 = no trial).</summary>
    public int TrialPeriodDays { get; set; }

    public string SuccessUrl { get; set; } = string.Empty;

    public string CancelUrl { get; set; } = string.Empty;

    public bool AllowPromotionCodes { get; set; } = false;

    /// <summary>Where the Billing Portal returns the user after they finish managing their subscription.</summary>
    public string PortalReturnUrl { get; set; } = string.Empty;
}

/// <summary>
/// Resolves per-product billing configuration by product slug. Backed by app configuration; the set of
/// known products is the set of products the gateway is willing to bill for, so unknown products are
/// rejected at the boundary rather than implicitly created.
/// </summary>
public interface IProductBillingRegistry
{
    /// <summary>True when a product slug has registered billing configuration.</summary>
    bool IsKnown(string productId);

    /// <summary>Gets a product's billing config, or throws if the product is not registered.</summary>
    ProductBillingConfig Get(string productId);
}
