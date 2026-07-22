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

        var customerReference = customerResult.ValueOrThrow;

        try
        {
            var url = await paymentGateway.CreateCheckoutSessionUrlAsync(
                request.ProductId, customerReference, cancellationToken);
            return Result<CheckoutSessionResponse>.Success(new CheckoutSessionResponse(url));
        }
        catch (PaymentCustomerNotFoundException)
        {
            // Our mapping points at a customer the provider no longer has (e.g. deleted directly in
            // the provider dashboard). Nothing depends on that customer id yet at this point in the
            // flow, so it's safe to mint a replacement and repoint the mapping rather than fail the
            // checkout outright.
            customerReference = await customerService.RecreateCustomerAsync(
                request.ProductId, request.UserId, request.Email, cancellationToken);

            var url = await paymentGateway.CreateCheckoutSessionUrlAsync(
                request.ProductId, customerReference, cancellationToken);
            return Result<CheckoutSessionResponse>.Success(new CheckoutSessionResponse(url));
        }
    }
}
