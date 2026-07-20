using SharedKernel.Results;
using Payments.Application.Abstractions;
using Payments.Domain.Entities;
using Payments.Domain.Errors;

namespace Payments.Infrastructure.Services;

public class CustomerService(
    IPaymentsRepository repository,
    IPaymentsUnitOfWork unitOfWork,
    IPaymentGateway paymentGateway) : ICustomerService
{
    public async Task<Result<string>> EnsureCustomerAsync(
        string productId, Guid userId, string email, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return Result<string>.Failure(PaymentsErrors.MissingUserId);
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<string>.Failure(PaymentsErrors.MissingEmail);
        }

        var existing = await repository.GetMappingByUserIdAsync(productId, userId, ct);
        if (existing is not null)
        {
            return Result<string>.Success(existing.CustomerReference);
        }

        var customerReference = await paymentGateway.CreateCustomerAsync(productId, userId, email, ct);

        await repository.AddMappingAsync(new CustomerMapping(productId, userId, customerReference), ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<string>.Success(customerReference);
    }
}
