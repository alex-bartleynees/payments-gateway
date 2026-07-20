using SharedKernel.Messaging.Abstractions;

namespace Common.IntegrationEvents.Payments;

/// <summary>
/// Published by the payment gateway whenever a customer's subscription is (re)synced, carrying the
/// canonical entitlement decision so consuming products keep a local read-model and gate locally
/// without ever calling the gateway or reading its database.
/// <para>
/// <see cref="Status"/> is the stable lower-case token (e.g. <c>past_due</c>), never a C# enum name —
/// the wire contract must not depend on any serializer's enum handling, and consumers do not reference
/// the gateway's domain. <see cref="HasAccess"/> is the gateway's authoritative access decision, so a
/// consumer can simply trust it rather than re-deriving the rule.
/// </para>
/// </summary>
[IntegrationEventRoutingKey("subscription.entitlement.changed")]
public record SubscriptionEntitlementChanged(
    Guid MessageId,
    string ProductId,
    Guid UserId,
    string Status,
    bool HasAccess,
    DateTimeOffset? CurrentPeriodEnd,
    bool CancelAtPeriodEnd) : IntegrationEvent;
