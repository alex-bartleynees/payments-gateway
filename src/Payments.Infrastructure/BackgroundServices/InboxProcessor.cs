using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payments.Application.Abstractions;
using Payments.Infrastructure.DbContexts;

namespace Payments.Infrastructure.BackgroundServices;

/// <summary>
/// Drains the durable inbox (<c>InboxMessages</c>): claims pending rows with <c>FOR UPDATE SKIP LOCKED</c>,
/// runs a full resync per customer, and marks them processed — all in one transaction so a crash rolls the
/// claim back and another instance/restart retries it. Safe to run on every instance simultaneously.
/// </summary>
public class InboxProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<InboxProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 10;
    private const int MaxAttempts = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedAny = await ProcessBatchAsync(stoppingToken);
                if (!processedAny)
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Inbox processing cycle failed");
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PaymentsContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IPaymentsRepository>();
        var syncService = scope.ServiceProvider.GetRequiredService<ISubscriptionSyncService>();

        // The context uses a retrying execution strategy, which forbids a user-initiated transaction
        // unless it's run inside the strategy so the whole unit can be retried atomically.
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            var messages = await repository.ClaimPendingInboxAsync(MaxAttempts, BatchSize, ct);
            if (messages.Count == 0)
            {
                await transaction.RollbackAsync(ct);
                return false;
            }

            foreach (var message in messages)
            {
                message.Attempts++;
                try
                {
                    // SyncAsync fetches from Stripe first, so a Stripe/network failure throws before any DB
                    // write and leaves this transaction intact — we then just persist the attempt + error.
                    await syncService.SyncAsync(message.CustomerReference, ct);
                    message.Processed = true;
                    message.ProcessedAt = DateTimeOffset.UtcNow;
                    message.LastError = null;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    message.LastError = Truncate(ex.Message, 500);
                    logger.LogError(
                        ex,
                        "Failed to sync customer {CustomerReference} (inbox event {EventReference}, attempt {Attempts})",
                        message.CustomerReference, message.EventReference, message.Attempts);
                }
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        });
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
