using Ardalis.GuardClauses;
using SharedKernel.Results;
using Mediator;
using Payments.Application.Abstractions;
using Payments.Domain.Entities;

namespace Payments.Application.Payments.Commands;

/// <summary>
/// Durably records a received webhook in the inbox before the endpoint acks Stripe with 200. Deduped
/// on the Stripe event id, so a redelivered webhook is a no-op. The actual resync happens later in the
/// inbox poller — this just guarantees the work is persisted and can't be lost on a crash/deploy. The
/// product is resolved from the customer mapping (falls back to empty when the customer is unknown —
/// the resync itself is still driven purely off the customer reference).
/// </summary>
public record RecordWebhookEvent(string EventReference, string EventType, string CustomerReference) : IRequest<Result>;

public class RecordWebhookEventHandler(
    IPaymentsRepository repository,
    IPaymentsUnitOfWork unitOfWork) : IRequestHandler<RecordWebhookEvent, Result>
{
    public async ValueTask<Result> Handle(RecordWebhookEvent request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(request);

        if (await repository.InboxEventExistsAsync(request.EventReference, cancellationToken))
        {
            return Result.Success();
        }

        var mapping = await repository.GetMappingByCustomerReferenceAsync(request.CustomerReference, cancellationToken);

        await repository.AddInboxMessageAsync(
            new InboxMessage
            {
                Id = Guid.NewGuid(),
                ProductId = mapping?.ProductId ?? string.Empty,
                EventReference = request.EventReference,
                EventType = request.EventType,
                CustomerReference = request.CustomerReference
            },
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
