using System.Text.Json.Serialization;

namespace Payments.Domain.Billing;

/// <summary>
/// Domain representation of a subscription's lifecycle state, translated from the payment provider's
/// raw status string inside the anti-corruption layer (the domain never sees the provider's own type).
/// Members are ordered so the enum's <c>default</c> (<see cref="Unknown"/>) denies access — a missing
/// or unmapped state can never accidentally grant entitlement.
/// </summary>
[JsonConverter(typeof(SubscriptionStatusJsonConverter))]
public enum SubscriptionStatus
{
    /// <summary>Provider status not recognised by the domain. Denies access.</summary>
    Unknown = 0,

    /// <summary>No subscription has ever been created for the customer.</summary>
    None,

    Trialing,
    Active,
    PastDue,
    Canceled,
    Unpaid,
    Incomplete,
    IncompleteExpired,
    Paused
}
