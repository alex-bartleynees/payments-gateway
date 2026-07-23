using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Gateway.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Payments.Domain.Billing;
using Payments.Domain.Entities;
using Payments.Infrastructure.DbContexts;
using Xunit;

namespace Gateway.IntegrationTests.Billing;

[Collection(IntegrationCollection.Name)]
public class BillingEndpointsTests(ApiTestFixture fixture)
{
    private const string Product = CustomWebApplicationFactory.ProductId;

    private record SubscriptionStateResponse(
        string Status,
        string? PriceId,
        DateTimeOffset? CurrentPeriodEnd,
        bool CancelAtPeriodEnd,
        string? PaymentMethodBrand,
        string? PaymentMethodLast4);

    [Fact]
    public async Task Subscription_state_requires_authentication()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync($"/api/billing/{Product}/subscription");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Portal_requires_authentication()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsync($"/api/billing/{Product}/portal", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Subscription_state_for_new_user_is_none()
    {
        var client = fixture.CreateClientAs(Guid.NewGuid());

        var response = await client.GetAsync($"/api/billing/{Product}/subscription");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = await response.Content.ReadFromJsonAsync<SubscriptionStateResponse>();
        state!.Status.Should().Be("none");
        state.CancelAtPeriodEnd.Should().BeFalse();
        state.PriceId.Should().BeNull();
    }

    [Fact]
    public async Task Unknown_product_is_rejected_with_404()
    {
        // The product slug is validated against the registry before any provider work.
        var client = fixture.CreateClientAs(Guid.NewGuid());

        var response = await client.GetAsync("/api/billing/not-a-real-product/subscription");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Subscription_state_serializes_status_as_the_contract_token()
    {
        // The domain models status as the SubscriptionStatus enum, but the frontend contract requires
        // the raw lower-case token. Seed a PastDue state and prove it round-trips through the EF value
        // converter (column → enum) and JSON (enum → token) as "past_due".
        var userId = Guid.NewGuid();
        var customerReference = $"cus_test_{Guid.NewGuid():N}";

        await fixture.WithScopeAsync(async sp =>
        {
            var context = sp.GetRequiredService<PaymentsContext>();
            context.CustomerMappings.Add(new CustomerMapping(Product, userId, customerReference));
            context.SubscriptionStates.Add(new SubscriptionState
            {
                CustomerReference = customerReference,
                ProductId = Product,
                Status = SubscriptionStatus.PastDue
            });
            await context.SaveChangesAsync();
        });

        var client = fixture.CreateClientAs(userId);
        var response = await client.GetAsync($"/api/billing/{Product}/subscription");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = await response.Content.ReadFromJsonAsync<SubscriptionStateResponse>();
        state!.Status.Should().Be("past_due", "the wire contract uses Stripe's lower-case status token");

        // The gateway's canonical access rule treats past_due as a soft-grace access state.
        SubscriptionStatus.PastDue.GrantsAccess().Should().BeTrue();
    }

    [Fact]
    public async Task Webhook_is_anonymous_and_rejects_an_invalid_signature()
    {
        // No auth header at all — the endpoint must be reachable (signature is the auth), and an
        // unverifiable payload must be rejected with 400, never 401.
        var client = fixture.Factory.CreateClient();

        var content = new StringContent(
            "{\"id\":\"evt_test\",\"type\":\"customer.subscription.updated\"}");
        content.Headers.Add("Stripe-Signature", "t=1,v1=not_a_valid_signature");

        var response = await client.PostAsync("/api/billing/webhook", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Webhook_rejects_an_oversized_body()
    {
        var client = fixture.Factory.CreateClient();
        var content = new StringContent(new string('x', 65 * 1024));
        content.Headers.Add("Stripe-Signature", "t=1,v1=not_a_valid_signature");

        var response = await client.PostAsync("/api/billing/webhook", content);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }
}
