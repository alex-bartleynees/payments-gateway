using Ardalis.GuardClauses;
using SharedKernel.Results;
using Mediator;
using Payments.Application.Abstractions;
using Payments.Application.Common.Models;

namespace Payments.Application.Payments.Commands;

/// <summary>
/// Ensures the customer exists, then creates a Checkout Session for it — always passing the
/// existing customer id explicitly so Checkout never implicitly creates a second one.
/// </summary>
public record CreateCheckoutSession(string ProductId, Guid UserId, string Email)
    : IRequest<Result<CheckoutSessionResponse>>;

public class CreateCheckoutSessionHandler(
    ICustomerService customerService,
    IPaymentGateway paymentGateway) : IRequestHandler<CreateCheckoutSession, Result<CheckoutSessionResponse>>
{
    public async ValueTask<Result<CheckoutSessionResponse>> Handle(
        CreateCheckoutSession request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(request);

        var customerResult = await customerService.EnsureCustomerAsync(
            request.ProductId, request.UserId, request.Email, cancellationToken);
        if (customerResult.IsFailure)
        {
            return Result<CheckoutSessionResponse>.Failure(customerResult.Error);
        }

        var url = await paymentGateway.CreateCheckoutSessionUrlAsync(
            request.ProductId, customerResult.ValueOrThrow, cancellationToken);
        return Result<CheckoutSessionResponse>.Success(new CheckoutSessionResponse(url));
    }
}
