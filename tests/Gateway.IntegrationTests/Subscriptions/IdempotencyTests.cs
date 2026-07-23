using FluentAssertions;
using Gateway.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Payments.Application.Abstractions;
using Payments.Domain.Entities;
using Payments.Infrastructure.DbContexts;
using Xunit;

namespace Gateway.IntegrationTests.Subscriptions;

[Collection(IntegrationCollection.Name)]
public class IdempotencyTests(ApiTestFixture fixture)
{
    [Fact]
    public async Task Concurrent_webhook_redeliveries_create_one_inbox_row()
    {
        var eventReference = $"evt_concurrent_{Guid.NewGuid():N}";

        var inserts = await Task.WhenAll(
            TryInsertInboxAsync(eventReference),
            TryInsertInboxAsync(eventReference));

        inserts.Should().ContainSingle(inserted => inserted);

        await fixture.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<PaymentsContext>();
            var count = await db.InboxMessages.CountAsync(x => x.EventReference == eventReference);
            count.Should().Be(1);
        });
    }

    [Fact]
    public async Task Concurrent_customer_mapping_inserts_select_one_winner()
    {
        var productId = CustomWebApplicationFactory.ProductId;
        var userId = Guid.NewGuid();

        var inserts = await Task.WhenAll(
            TryInsertMappingAsync(new CustomerMapping(productId, userId, $"cus_{Guid.NewGuid():N}")),
            TryInsertMappingAsync(new CustomerMapping(productId, userId, $"cus_{Guid.NewGuid():N}")));

        inserts.Should().ContainSingle(inserted => inserted);

        await fixture.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<PaymentsContext>();
            var count = await db.CustomerMappings.CountAsync(
                x => x.ProductId == productId && x.UserId == userId);
            count.Should().Be(1);
        });
    }

    private async Task<bool> TryInsertInboxAsync(string eventReference)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPaymentsRepository>();
        return await repository.TryAddInboxMessageAsync(new InboxMessage
        {
            Id = Guid.NewGuid(),
            ProductId = CustomWebApplicationFactory.ProductId,
            EventReference = eventReference,
            EventType = "customer.subscription.updated",
            CustomerReference = $"cus_{Guid.NewGuid():N}"
        });
    }

    private async Task<bool> TryInsertMappingAsync(CustomerMapping mapping)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPaymentsRepository>();
        return await repository.TryAddMappingAsync(mapping);
    }
}
