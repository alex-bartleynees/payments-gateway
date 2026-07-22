using Microsoft.Extensions.Options;
using Payments.Application.Abstractions;
using Payments.Application.Common.Models;
using Payments.Domain.Billing;
using Payments.Infrastructure.Configuration;
using Stripe;

namespace Payments.Infrastructure.Services;

/// <summary>
/// Stripe implementation of <see cref="IPaymentGateway"/> — the anti-corruption layer: the only place
/// Stripe SDK types are used, translating them into the domain's provider-agnostic models. Global
/// credentials come from <see cref="StripeOptions"/>; per-product price, URLs and trial length are
/// looked up by <c>productId</c> from the <see cref="IProductBillingRegistry"/>, so a single Stripe
/// account bills many products off distinct Prices.
/// </summary>
public class StripePaymentGateway : IPaymentGateway
{
    private readonly StripeOptions _options;
    private readonly IProductBillingRegistry _products;
    private readonly IStripeClient _client;

    public StripePaymentGateway(IOptions<StripeOptions> options, IProductBillingRegistry products)
    {
        _options = options.Value;
        _products = products;
        _client = new StripeClient(_options.SecretKey);
    }

    public async Task<string> CreateCustomerAsync(string productId, Guid userId, string email, CancellationToken ct = default)
    {
        var options = new CustomerCreateOptions
        {
            Email = email,
            Metadata = new Dictionary<string, string>
            {
                ["productId"] = productId,
                ["userId"] = userId.ToString()
            }
        };

        var customer = await new Stripe.CustomerService(_client).CreateAsync(options, cancellationToken: ct);
        return customer.Id;
    }

    public async Task<string> CreateCheckoutSessionUrlAsync(string productId, string customerReference, CancellationToken ct = default)
    {
        var product = _products.Get(productId);

        var options = new Stripe.Checkout.SessionCreateOptions
        {
            Customer = customerReference,
            Mode = "subscription",
            LineItems =
            [
                new Stripe.Checkout.SessionLineItemOptions { Price = product.PriceId, Quantity = 1 }
            ],
            SuccessUrl = product.SuccessUrl,
            CancelUrl = product.CancelUrl,
            AllowPromotionCodes = product.AllowPromotionCodes,
            // Stamp the subscription so provider-side tooling (and any webhook that inspects metadata)
            // can attribute it to a product; routing in this service stays driven off the customer mapping.
            SubscriptionData = new Stripe.Checkout.SessionSubscriptionDataOptions
            {
                TrialPeriodDays = product.TrialPeriodDays > 0 ? product.TrialPeriodDays : null,
                Metadata = new Dictionary<string, string> { ["productId"] = productId }
            }
        };

        var session = await new Stripe.Checkout.SessionService(_client).CreateAsync(options, cancellationToken: ct);
        return session.Url;
    }

    public async Task<string> CreatePortalSessionUrlAsync(string productId, string customerReference, CancellationToken ct = default)
    {
        var product = _products.Get(productId);

        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = customerReference,
            ReturnUrl = product.PortalReturnUrl
        };

        var session = await new Stripe.BillingPortal.SessionService(_client).CreateAsync(options, cancellationToken: ct);
        return session.Url;
    }

    public async Task<SubscriptionSnapshot?> GetLatestSubscriptionAsync(
        string customerReference, CancellationToken ct = default)
    {
        var options = new SubscriptionListOptions
        {
            Customer = customerReference,
            Status = "all",
            Limit = 1
        };
        options.AddExpand("data.default_payment_method");

        var subscriptions = await new SubscriptionService(_client).ListAsync(options, cancellationToken: ct);
        var subscription = subscriptions.Data.FirstOrDefault();
        if (subscription is null)
        {
            return null;
        }

        // As of Stripe API 2025-03-31 the period boundaries live on the subscription item, not the subscription.
        var item = subscription.Items?.Data?.FirstOrDefault();
        var card = subscription.DefaultPaymentMethod?.Card;

        return new SubscriptionSnapshot(
            subscription.Id,
            SubscriptionStatusTokens.FromProviderStatus(subscription.Status),
            item?.Price?.Id,
            ToUtc(item?.CurrentPeriodStart),
            ToUtc(item?.CurrentPeriodEnd),
            subscription.CancelAtPeriodEnd,
            card?.Brand,
            card?.Last4);
    }

    public PaymentProviderNotification? ParseWebhookEvent(string payload, string signature)
    {
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(payload, signature, _options.WebhookSecret);

            var customerReference = stripeEvent.Data.Object switch
            {
                Subscription subscription => subscription.CustomerId,
                Stripe.Checkout.Session session => session.CustomerId,
                Invoice invoice => invoice.CustomerId,
                PaymentIntent paymentIntent => paymentIntent.CustomerId,
                _ => null
            };

            return new PaymentProviderNotification(stripeEvent.Id, stripeEvent.Type, customerReference);
        }
        catch (StripeException)
        {
            return null;
        }
    }

    private static DateTimeOffset? ToUtc(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
}
