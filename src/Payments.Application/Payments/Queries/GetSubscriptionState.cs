using Mediator;
using Payments.Application.Abstractions;
using Payments.Application.Common.Models;
using Payments.Domain.Billing;

namespace Payments.Application.Payments.Queries;

/// <summary>
/// Read model for the account/settings page. Always returns a DTO — a user with no customer or no
/// subscription yet gets the <c>none</c> shape rather than a 404, so the frontend can show a CTA.
/// </summary>
public record GetSubscriptionState(string ProductId, Guid UserId) : IRequest<SubscriptionStateDto>;

public class GetSubscriptionStateHandler(IPaymentsRepository repository)
    : IRequestHandler<GetSubscriptionState, SubscriptionStateDto>
{
    private static readonly SubscriptionStateDto NoneState =
        new(SubscriptionStatus.None, null, null, false, null, null);

    public async ValueTask<SubscriptionStateDto> Handle(GetSubscriptionState request, CancellationToken cancellationToken)
    {
        var mapping = await repository.GetMappingByUserIdAsync(request.ProductId, request.UserId, cancellationToken);
        if (mapping is null)
        {
            return NoneState;
        }

        var state = await repository.GetSubscriptionStateAsync(mapping.CustomerReference, cancellationToken);
        if (state is null)
        {
            return NoneState;
        }

        return new SubscriptionStateDto(
            state.Status,
            state.PriceId,
            state.CurrentPeriodEnd,
            state.CancelAtPeriodEnd,
            state.PaymentMethodBrand,
            state.PaymentMethodLast4);
    }
}
