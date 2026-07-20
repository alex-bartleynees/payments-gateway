using Payments.Domain.Entities;

namespace Payments.Application.Abstractions;

public interface IPaymentsRepository
{
    Task<CustomerMapping?> GetMappingByUserIdAsync(string productId, Guid userId, CancellationToken ct = default);

    /// <summary>Resolves the mapping (and thus its product + user) from the globally-unique customer reference.</summary>
    Task<CustomerMapping?> GetMappingByCustomerReferenceAsync(string customerReference, CancellationToken ct = default);

    Task AddMappingAsync(CustomerMapping mapping, CancellationToken ct = default);

    /// <summary>Tracked read — callers mutate the returned entity and commit via the unit of work.</summary>
    Task<SubscriptionState?> GetSubscriptionStateAsync(string customerReference, CancellationToken ct = default);

    Task AddSubscriptionStateAsync(SubscriptionState state, CancellationToken ct = default);

    Task<bool> InboxEventExistsAsync(string eventReference, CancellationToken ct = default);

    Task AddInboxMessageAsync(InboxMessage message, CancellationToken ct = default);

    /// <summary>Enqueues a transactional-outbox row; committed atomically with the state overwrite.</summary>
    Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken ct = default);

    /// <summary>
    /// Claims up to <paramref name="batchSize"/> pending inbox rows for exclusive processing using
    /// <c>FOR UPDATE SKIP LOCKED</c>. Must be called inside a transaction owned by the caller: the row
    /// locks are held until that transaction commits, so other instances skip these rows rather than
    /// double-processing them. Rows already retried <paramref name="maxAttempts"/> times are excluded
    /// (left as dead letters). Returned entities are tracked, so the caller mutates and saves them.
    /// </summary>
    Task<List<InboxMessage>> ClaimPendingInboxAsync(int maxAttempts, int batchSize, CancellationToken ct = default);
}
