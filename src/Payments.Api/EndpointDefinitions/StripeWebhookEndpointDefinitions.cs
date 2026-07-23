using SharedKernel.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Payments.Application.Abstractions;
using Payments.Application.Payments.Commands;

namespace Payments.Api.EndpointDefinitions;

/// <summary>
/// Unauthenticated (the Stripe signature is the auth). Verifies the signature, and for a tracked
/// event type enqueues a resync of that customer — the payload itself is never parsed for business
/// logic. No per-event branching: the sync is idempotent and always reflects Stripe's current truth.
/// A single endpoint serves every product; the product is resolved from the customer mapping at sync
/// time, so no product scoping is needed on the URL.
/// </summary>
public class StripeWebhookEndpointDefinitions : IEndpointDefinition
{
    private const int MaxWebhookBodyBytes = 64 * 1024;
    // Every one of these just means "this customer's subscription may have changed — resync it".
    private static readonly HashSet<string> HandledEvents =
    [
        "checkout.session.completed",
        "customer.subscription.created",
        "customer.subscription.updated",
        "customer.subscription.deleted",
        "customer.subscription.paused",
        "customer.subscription.resumed",
        "customer.subscription.pending_update_applied",
        "customer.subscription.pending_update_expired",
        "customer.subscription.trial_will_end",
        "invoice.paid",
        "invoice.payment_failed",
        "invoice.payment_action_required",
        "invoice.upcoming",
        "invoice.marked_uncollectible",
        "invoice.payment_succeeded",
        "payment_intent.succeeded",
        "payment_intent.payment_failed",
        "payment_intent.canceled"
    ];

    public void RegisterEndpoints(WebApplication app)
    {
        app.MapPost("api/billing/webhook", HandleWebhook)
            .AllowAnonymous()
            .WithMetadata(new RequestSizeLimitAttribute(MaxWebhookBodyBytes))
            .WithName("StripeWebhook");
    }

    private static async Task<IResult> HandleWebhook(
        HttpRequest request,
        IPaymentGateway paymentGateway,
        Payments.Api.Mediator.Mediator mediator,
        ILogger<StripeWebhookEndpointDefinitions> logger)
    {
        if (request.ContentLength > MaxWebhookBodyBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        using var reader = new StreamReader(request.Body);
        var json = await reader.ReadToEndAsync();
        var signature = request.Headers["Stripe-Signature"].ToString();

        var notification = paymentGateway.ParseWebhookEvent(json, signature);
        if (notification is null)
        {
            logger.LogWarning("Rejected Stripe webhook: signature verification failed");
            return Results.BadRequest();
        }

        if (HandledEvents.Contains(notification.EventType) && !string.IsNullOrEmpty(notification.CustomerReference))
        {
            await mediator.Send(new RecordWebhookEvent(
                notification.EventReference, notification.EventType, notification.CustomerReference));

            logger.LogInformation(
                "Recorded resync for customer {CustomerReference} from {EventType} ({EventReference})",
                notification.CustomerReference, notification.EventType, notification.EventReference);
        }

        return Results.Ok();
    }
}
