using Microsoft.EntityFrameworkCore;
using Payments.Application.Abstractions;
using Payments.Domain.Entities;
using Payments.Infrastructure.DbContexts;

namespace Payments.Infrastructure.Repositories;

public class PaymentsRepository(PaymentsContext context) : IPaymentsRepository
{
    public async Task<CustomerMapping?> GetMappingByUserIdAsync(string productId, Guid userId, CancellationToken ct = default)
    {
        return await context.CustomerMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ProductId == productId && m.UserId == userId, ct);
    }

    public async Task<CustomerMapping?> GetMappingByCustomerReferenceAsync(string customerReference, CancellationToken ct = default)
    {
        return await context.CustomerMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CustomerReference == customerReference, ct);
    }

    public async Task<bool> TryAddMappingAsync(CustomerMapping mapping, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "CustomerMappings"
                 ("ProductId", "UserId", "CustomerReference", "CreatedAt", "UpdatedAt")
             VALUES
                 ({mapping.ProductId}, {mapping.UserId}, {mapping.CustomerReference}, {now}, {now})
             ON CONFLICT DO NOTHING
             """, ct);

        return rows == 1;
    }

    public async Task UpdateMappingCustomerReferenceAsync(
        string productId, Guid userId, string customerReference, CancellationToken ct = default)
    {
        var mapping = await context.CustomerMappings
            .FirstOrDefaultAsync(m => m.ProductId == productId && m.UserId == userId, ct);

        if (mapping is not null)
        {
            mapping.CustomerReference = customerReference;
        }
    }

    public async Task<SubscriptionState?> GetSubscriptionStateAsync(string customerReference, CancellationToken ct = default)
    {
        return await context.SubscriptionStates
            .FirstOrDefaultAsync(s => s.CustomerReference == customerReference, ct);
    }

    public async Task AddSubscriptionStateAsync(SubscriptionState state, CancellationToken ct = default)
    {
        await context.SubscriptionStates.AddAsync(state, ct);
    }

    public async Task<bool> InboxEventExistsAsync(string eventReference, CancellationToken ct = default)
    {
        return await context.InboxMessages
            .AsNoTracking()
            .AnyAsync(m => m.EventReference == eventReference, ct);
    }

    public async Task<bool> TryAddInboxMessageAsync(InboxMessage message, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "InboxMessages"
                 ("Id", "ProductId", "EventReference", "EventType", "CustomerReference",
                  "Processed", "ProcessedAt", "Attempts", "LastError", "CreatedAt", "UpdatedAt")
             VALUES
                 ({message.Id}, {message.ProductId}, {message.EventReference}, {message.EventType},
                  {message.CustomerReference}, false, NULL, 0, NULL, {now}, {now})
             ON CONFLICT ("EventReference") DO NOTHING
             """, ct);

        return rows == 1;
    }

    public async Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken ct = default)
    {
        await context.OutboxMessages.AddAsync(message, ct);
    }

    public async Task<List<InboxMessage>> ClaimPendingInboxAsync(
        int maxAttempts, int batchSize, CancellationToken ct = default)
    {
        // FOR UPDATE SKIP LOCKED: rows locked by another instance's in-flight transaction are skipped
        // rather than blocked on, so instances share the backlog without ever double-processing a row.
        return await context.InboxMessages
            .FromSqlRaw(
                """
                SELECT * FROM "InboxMessages"
                WHERE "Processed" = false AND "Attempts" < {0}
                ORDER BY "CreatedAt"
                LIMIT {1}
                FOR UPDATE SKIP LOCKED
                """,
                maxAttempts, batchSize)
            .ToListAsync(ct);
    }
}
