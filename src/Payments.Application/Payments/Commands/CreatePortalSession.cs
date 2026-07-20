using Ardalis.GuardClauses;
using SharedKernel.Results;
using Mediator;
using Payments.Application.Abstractions;
using Payments.Application.Common.Models;
using Payments.Domain.Errors;

namespace Payments.Application.Payments.Commands;

/// <summary>
/// Creates a Billing Portal session for the caller's own customer. The customer id is resolved
/// server-side from the (product, authenticated user) — never supplied by the client.
/// </summary>
public record CreatePortalSession(string ProductId, Guid UserId) : IRequest<Result<PortalSessionResponse>>;

public class CreatePortalSessionHandler(
    IPaymentsRepository repository,
    IPaymentGateway paymentGateway) : IRequestHandler<CreatePortalSession, Result<PortalSessionResponse>>
{
    public async ValueTask<Result<PortalSessionResponse>> Handle(
        CreatePortalSession request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(request);

        var mapping = await repository.GetMappingByUserIdAsync(request.ProductId, request.UserId, cancellationToken);
        if (mapping is null)
        {
            return Result<PortalSessionResponse>.Failure(PaymentsErrors.NoCustomer);
        }

        var url = await paymentGateway.CreatePortalSessionUrlAsync(
            request.ProductId, mapping.CustomerReference, cancellationToken);
        return Result<PortalSessionResponse>.Success(new PortalSessionResponse(url));
    }
}
