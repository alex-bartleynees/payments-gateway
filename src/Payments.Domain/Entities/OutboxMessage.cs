using SharedKernel.Abstractions;

namespace Payments.Domain.Entities;

/// <summary>
/// Transactional outbox row. <c>SubscriptionSyncService</c> writes one of these in the same
/// transaction as the <see cref="SubscriptionState"/> overwrite, and the <c>OutboxPublisher</c> later
/// publishes it to RabbitMQ with <c>FOR UPDATE SKIP LOCKED</c> — so an entitlement change is never
/// lost between the DB commit and the broker, and never double-published across instances.
/// </summary>
public class OutboxMessage : IAuditable
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Published { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
