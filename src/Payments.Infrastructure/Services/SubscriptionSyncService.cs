using System.Text.Json;
using Common.IntegrationEvents.Payments;
using Microsoft.Extensions.Logging;
using Payments.Application.Abstractions;
using Payments.Domain.Billing;
using Payments.Domain.Entities;

namespace Payments.Infrastructure.Services;

/// <inheritdoc cref="ISubscriptionSyncService"/>
public class SubscriptionSyncService(
    IPaymentsRepository repository,
    IPaymentGateway paymentGateway,
    IPaymentsUnitOfWork unitOfWork,
    ILogger<SubscriptionSyncService> logger) : ISubscriptionSyncService
{
    public async Task SyncAsync(string customerReference, CancellationToken ct = default)
    {
        // The mapping is the authoritative (product, user) for this customer — never trusted from a
        // webhook payload. A customer with no mapping (unknown to us) is still overwritten locally, but
        // there's nobody to attribute an entitlement event to, so we skip publishing in that case.
        var mapping = await repository.GetMappingByCustomerReferenceAsync(customerReference, ct);

        var snapshot = await paymentGateway.GetLatestSubscriptionAsync(customerReference, ct);

        var state = await repository.GetSubscriptionStateAsync(customerReference, ct);
        if (state is null)
        {
            state = new SubscriptionState { CustomerReference = customerReference };
            await repository.AddSubscriptionStateAsync(state, ct);
        }

        if (mapping is not null)
        {
            state.ProductId = mapping.ProductId;
        }

        // Full overwrite from the provider's current truth — never a partial patch from a webhook payload.
        if (snapshot is null)
        {
            state.SubscriptionReference = null;
            state.Status = SubscriptionStatus.None;
            state.PriceId = null;
            state.CurrentPeriodStart = null;
            state.CurrentPeriodEnd = null;
            state.CancelAtPeriodEnd = false;
            state.PaymentMethodBrand = null;
            state.PaymentMethodLast4 = null;
        }
        else
        {
            state.SubscriptionReference = snapshot.SubscriptionReference;
            state.Status = snapshot.Status;
            state.PriceId = snapshot.PriceId;
            state.CurrentPeriodStart = snapshot.CurrentPeriodStart;
            state.CurrentPeriodEnd = snapshot.CurrentPeriodEnd;
            state.CancelAtPeriodEnd = snapshot.CancelAtPeriodEnd;
            state.PaymentMethodBrand = snapshot.PaymentMethodBrand;
            state.PaymentMethodLast4 = snapshot.PaymentMethodLast4;
        }

        // Entitlement event written to the outbox in the same transaction as the state overwrite, so the
        // published decision can never diverge from the stored one. HasAccess is the gateway's canonical
        // rule (GrantsAccess); Status rides the wire as its stable lower-case token.
        if (mapping is not null)
        {
            await EnqueueEntitlementEventAsync(mapping.ProductId, mapping.UserId, state, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Synced subscription state for customer {CustomerReference}: status={Status}",
            customerReference, state.Status);
    }

    private async Task EnqueueEntitlementEventAsync(
        string productId, Guid userId, SubscriptionState state, CancellationToken ct)
    {
        var @event = new SubscriptionEntitlementChanged(
            MessageId: Guid.NewGuid(),
            ProductId: productId,
            UserId: userId,
            Status: state.Status.ToToken(),
            HasAccess: state.Status.GrantsAccess(),
            CurrentPeriodEnd: state.CurrentPeriodEnd,
            CancelAtPeriodEnd: state.CancelAtPeriodEnd);

        await repository.AddOutboxMessageAsync(
            new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageId = @event.MessageId,
                Type = typeof(SubscriptionEntitlementChanged).AssemblyQualifiedName!,
                Payload = JsonSerializer.Serialize(@event)
            },
            ct);
    }
}
