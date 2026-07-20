using SharedKernel.Abstractions;
using Payments.Domain.Billing;

namespace Payments.Domain.Entities;

/// <summary>
/// Local mirror of a customer's current subscription, keyed on <see cref="CustomerReference"/> (the
/// opaque provider customer id, globally unique within the one provider account). This row is
/// <em>always fully overwritten</em> by <c>SubscriptionSyncService</c> from the provider's current
/// truth — it is never partially patched from individual webhook payloads. <see cref="ProductId"/> is
/// denormalised from the customer mapping for per-product scoping and entitlement publishing.
/// </summary>
public class SubscriptionState : IAuditable
{
    /// <summary>Opaque provider customer reference (Stripe <c>cus_…</c>); the domain stores but never interprets it.</summary>
    public string CustomerReference { get; set; } = string.Empty;

    /// <summary>Product slug this subscription belongs to (e.g. <c>dopamine-kick</c>).</summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>Opaque provider subscription reference (Stripe <c>sub_…</c>); null when there is no subscription.</summary>
    public string? SubscriptionReference { get; set; }

    /// <summary>Domain subscription state, translated from the provider's raw status in the ACL.</summary>
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.None;

    public string? PriceId { get; set; }

    public DateTimeOffset? CurrentPeriodStart { get; set; }

    public DateTimeOffset? CurrentPeriodEnd { get; set; }

    public bool CancelAtPeriodEnd { get; set; }

    public string? PaymentMethodBrand { get; set; }

    public string? PaymentMethodLast4 { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
