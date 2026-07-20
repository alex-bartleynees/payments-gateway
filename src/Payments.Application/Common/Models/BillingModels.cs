using Payments.Domain.Billing;

namespace Payments.Application.Common.Models;

/// <summary>Provider-agnostic snapshot of a subscription, translated inside the payment gateway adapter.</summary>
public record SubscriptionSnapshot(
    string SubscriptionReference,
    SubscriptionStatus Status,
    string? PriceId,
    DateTimeOffset? CurrentPeriodStart,
    DateTimeOffset? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    string? PaymentMethodBrand,
    string? PaymentMethodLast4);

/// <summary>What a verified webhook boils down to: which customer to resync. The type is kept for logging
/// only; the event reference is the dedup key for the inbox.</summary>
public record PaymentProviderNotification(string EventReference, string EventType, string? CustomerReference);

public record CheckoutSessionResponse(string Url);

public record PortalSessionResponse(string Url);

/// <summary>Read model returned to the frontend for the account/settings page.</summary>
public record SubscriptionStateDto(
    SubscriptionStatus Status,
    string? PriceId,
    DateTimeOffset? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    string? PaymentMethodBrand,
    string? PaymentMethodLast4);
