using Ardalis.GuardClauses;
using SharedKernel.Results;
using Mediator;
using Payments.Application.Abstractions;
using Payments.Domain.Errors;

namespace Payments.Application.Payments.Commands;

/// <summary>
/// Resolves the caller's customer for the product and runs a full resync. Called by the /success
/// redirect handler before showing "you're subscribed", closing the race where the browser beats
/// Stripe's webhook.
/// </summary>
public record SyncSubscription(string ProductId, Guid UserId) : IRequest<Result>;

public class SyncSubscriptionHandler(
    IPaymentsRepository repository,
    ISubscriptionSyncService syncService) : IRequestHandler<SyncSubscription, Result>
{
    public async ValueTask<Result> Handle(SyncSubscription request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(request);

        var mapping = await repository.GetMappingByUserIdAsync(request.ProductId, request.UserId, cancellationToken);
        if (mapping is null)
        {
            return Result.Failure(PaymentsErrors.NoCustomer);
        }

        await syncService.SyncAsync(mapping.CustomerReference, cancellationToken);
        return Result.Success();
    }
}
