using System.Text.Json;
using SharedKernel.Messaging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payments.Domain.Entities;
using Payments.Infrastructure.DbContexts;

namespace Payments.Infrastructure.BackgroundServices;

/// <summary>
/// Publishes transactional-outbox rows to RabbitMQ. Claims unpublished rows with
/// <c>FOR UPDATE SKIP LOCKED</c> so multiple instances share the backlog without double-publishing,
/// resolves each event's routing key from its <see cref="IntegrationEventRoutingKeyAttribute"/>, and
/// marks them published in the same transaction.
/// </summary>
public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IMessagePublisher messagePublisher,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessages(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessOutboxMessages(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PaymentsContext>();

        // The context uses a retrying execution strategy, which forbids a user-initiated transaction
        // unless it's run inside the strategy so the whole unit can be retried atomically.
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            // FOR UPDATE SKIP LOCKED so multiple instances share the outbox without double-publishing:
            // rows locked by another instance's in-flight transaction are skipped rather than re-sent.
            var messages = await context.OutboxMessages
                .FromSqlRaw(
                    """
                    SELECT * FROM "OutboxMessages"
                    WHERE "Published" = false
                    ORDER BY "CreatedAt"
                    LIMIT 50
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(ct);

            foreach (var message in messages)
            {
                try
                {
                    var (@event, routingKey) = DeserializeEvent(message);

                    await messagePublisher.PublishAsync(@event, routingKey, cancellationToken: ct);

                    message.Published = true;
                    message.PublishedAt = DateTimeOffset.UtcNow;

                    logger.LogInformation(
                        "Published {Type} with MessageId {MessageId} to RabbitMQ with routing key {RoutingKey}",
                        message.Type,
                        message.MessageId,
                        routingKey);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process outbox message {MessageId}", message.MessageId);
                }
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }

    private (object Event, string RoutingKey) DeserializeEvent(OutboxMessage message)
    {
        var eventType = Type.GetType(message.Type)
                        ?? throw new InvalidOperationException($"Cannot resolve type: {message.Type}");

        var @event = JsonSerializer.Deserialize(message.Payload, eventType)
                     ?? throw new InvalidOperationException("Deserialization failed");

        var routingKey = GetRoutingKey(eventType);

        return (@event, routingKey);
    }

    private static string GetRoutingKey(Type eventType)
    {
        var attribute = eventType.GetCustomAttributes(typeof(IntegrationEventRoutingKeyAttribute), false)
            .FirstOrDefault() as IntegrationEventRoutingKeyAttribute;

        return attribute?.RoutingKey
               ?? throw new InvalidOperationException(
                   $"Integration event {eventType.Name} must have [IntegrationEventRoutingKey] attribute");
    }
}
