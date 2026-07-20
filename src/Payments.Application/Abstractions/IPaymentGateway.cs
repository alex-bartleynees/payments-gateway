using Payments.Application.Common.Models;

namespace Payments.Application.Abstractions;

/// <summary>
/// The domain's port onto the payment provider — the anti-corruption layer boundary. Named after the
/// capability it provides, not the vendor behind it; the only implementation
/// (<c>StripePaymentGateway</c>) confines every provider SDK type. Per-product price, URLs and trial
/// length are looked up by <c>productId</c> inside the implementation; the global provider secret and
/// webhook secret stay implementation-private, so callers only pass the product slug and the opaque
/// customer reference.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Creates a provider customer for the user and returns its opaque reference. Stamps the
    /// customer with <c>productId</c> + <c>userId</c> metadata for provider-side diagnostics.</summary>
    Task<string> CreateCustomerAsync(string productId, Guid userId, string email, CancellationToken ct = default);

    Task<string> CreateCheckoutSessionUrlAsync(string productId, string customerReference, CancellationToken ct = default);

    Task<string> CreatePortalSessionUrlAsync(string productId, string customerReference, CancellationToken ct = default);

    /// <summary>
    /// Fetches the customer's most recent subscription and translates it to a domain snapshot.
    /// Returns <c>null</c> when the customer has never had a subscription.
    /// </summary>
    Task<SubscriptionSnapshot?> GetLatestSubscriptionAsync(string customerReference, CancellationToken ct = default);

    /// <summary>
    /// Verifies the provider signature and returns the event reference, type and customer reference,
    /// or <c>null</c> if the signature is invalid. The payload is never trusted beyond routing to a resync.
    /// </summary>
    PaymentProviderNotification? ParseWebhookEvent(string payload, string signature);
}
