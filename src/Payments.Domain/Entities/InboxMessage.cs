using SharedKernel.Abstractions;

namespace Payments.Domain.Entities;

/// <summary>
/// Durable record of a received provider webhook (inbox / idempotent-receiver pattern). The webhook
/// handler writes this row before acknowledging the provider with 200, so the "resync this customer"
/// work survives a crash or rolling deploy — unlike an in-process queue. A poller drains unprocessed
/// rows with <c>FOR UPDATE SKIP LOCKED</c>, making processing safe and non-duplicated across instances.
/// <para>
/// <see cref="EventReference"/> is uniquely constrained, so a redelivered webhook is a no-op insert.
/// The payload itself is never stored or parsed for business logic — only which customer to resync.
/// <see cref="ProductId"/> is denormalised for scoping/diagnostics; the authoritative product for a
/// customer is always the customer mapping resolved at sync time.
/// </para>
/// </summary>
public class InboxMessage : IAuditable
{
    public Guid Id { get; set; }

    /// <summary>Product slug this event belongs to, resolved from the customer mapping when recorded.</summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>Opaque provider event reference (Stripe <c>evt_…</c>); unique — the dedup key for redelivered webhooks.</summary>
    public string EventReference { get; set; } = string.Empty;

    /// <summary>Retained for logging/diagnostics only; never branched on.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Opaque provider customer reference to resync.</summary>
    public string CustomerReference { get; set; } = string.Empty;

    public bool Processed { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Incremented each processing attempt; once it reaches the poller's max, the row is left as a dead letter.</summary>
    public int Attempts { get; set; }

    /// <summary>Last failure message, for diagnostics. Null while pending or after a successful sync.</summary>
    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
