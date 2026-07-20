namespace Payments.Application.Abstractions;

/// <summary>
/// The single source-of-truth writer for <c>SubscriptionState</c>. Re-fetches the full
/// subscription from the payment provider and overwrites the local row, then enqueues a
/// <c>SubscriptionEntitlementChanged</c> outbox row in the same transaction. Idempotent by
/// construction, so it can be called from the /success redirect and from the webhook inbox in any
/// order, any number of times, without producing split-brain state. The product and user are resolved
/// from the customer mapping, so callers only supply the opaque customer reference.
/// </summary>
public interface ISubscriptionSyncService
{
    Task SyncAsync(string customerReference, CancellationToken ct = default);
}
