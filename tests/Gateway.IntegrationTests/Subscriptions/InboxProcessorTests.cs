using FluentAssertions;
using Gateway.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Payments.Application.Abstractions;
using Payments.Domain.Entities;
using Payments.Infrastructure.DbContexts;
using Xunit;

namespace Gateway.IntegrationTests.Subscriptions;

[Collection(IntegrationCollection.Name)]
public class InboxProcessorTests(ApiTestFixture fixture)
{
    [Fact]
    public async Task Claim_query_returns_pending_rows_via_skip_locked()
    {
        var eventId = $"evt_test_{Guid.NewGuid():N}";

        // Seed the row already at the poller's max attempts so the live background InboxProcessor won't
        // claim it (its query filters Attempts < max). That keeps this test deterministic and independent
        // of any real Stripe backend — we only want to prove the FOR UPDATE SKIP LOCKED SQL executes and
        // returns pending rows against real Postgres.
        await fixture.WithScopeAsync(async sp =>
        {
            var context = sp.GetRequiredService<PaymentsContext>();
            context.InboxMessages.Add(new InboxMessage
            {
                Id = Guid.NewGuid(),
                ProductId = CustomWebApplicationFactory.ProductId,
                EventReference = eventId,
                EventType = "customer.subscription.updated",
                CustomerReference = $"cus_test_{Guid.NewGuid():N}",
                Attempts = 10
            });
            await context.SaveChangesAsync();
        });

        List<InboxMessage> claimed = [];
        await fixture.WithScopeAsync(async sp =>
        {
            var repository = sp.GetRequiredService<IPaymentsRepository>();
            // Higher threshold than the poller uses, so the seeded (dead-lettered) row is visible here.
            claimed = await repository.ClaimPendingInboxAsync(maxAttempts: 100, batchSize: 100);
        });

        claimed.Should().Contain(m => m.EventReference == eventId,
            "the SKIP LOCKED claim query must run against Postgres and return pending rows");
    }
}
