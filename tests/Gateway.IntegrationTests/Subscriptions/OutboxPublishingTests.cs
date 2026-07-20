using System.Text.Json;
using Common.IntegrationEvents.Payments;
using FluentAssertions;
using Gateway.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Payments.Domain.Entities;
using Payments.Infrastructure.DbContexts;
using Xunit;

namespace Gateway.IntegrationTests.Subscriptions;

[Collection(IntegrationCollection.Name)]
public class OutboxPublishingTests(ApiTestFixture fixture)
{
    [Fact]
    public async Task Entitlement_outbox_row_is_published_to_rabbitmq()
    {
        // A full sync would need a Stripe backend, so we seed the outbox row the sync service would write
        // and prove the OutboxPublisher drains it to the real RabbitMQ container (marking it Published).
        var userId = Guid.NewGuid();
        var @event = new SubscriptionEntitlementChanged(
            MessageId: Guid.NewGuid(),
            ProductId: CustomWebApplicationFactory.ProductId,
            UserId: userId,
            Status: "active",
            HasAccess: true,
            CurrentPeriodEnd: DateTimeOffset.UtcNow.AddDays(30),
            CancelAtPeriodEnd: false);

        await fixture.WithScopeAsync(async sp =>
        {
            var context = sp.GetRequiredService<PaymentsContext>();
            context.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageId = @event.MessageId,
                Type = typeof(SubscriptionEntitlementChanged).AssemblyQualifiedName!,
                Payload = JsonSerializer.Serialize(@event)
            });
            await context.SaveChangesAsync();
        });

        // The OutboxPublisher polls every ~5s; wait for it to publish to the real RabbitMQ container.
        var published = await WaitForPublishedAsync(@event.MessageId, TimeSpan.FromSeconds(30));

        published.Should().BeTrue(
            "the OutboxPublisher should have published the SubscriptionEntitlementChanged event to RabbitMQ");
    }

    private async Task<bool> WaitForPublishedAsync(Guid messageId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var published = false;
            await fixture.WithScopeAsync(async sp =>
            {
                var db = sp.GetRequiredService<PaymentsContext>();
                published = await db.OutboxMessages
                    .AsNoTracking()
                    .AnyAsync(m => m.MessageId == messageId && m.Published);
            });

            if (published)
            {
                return true;
            }

            await Task.Delay(500);
        }

        return false;
    }
}
