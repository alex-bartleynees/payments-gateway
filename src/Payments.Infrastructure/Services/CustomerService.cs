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

        // A stable provider idempotency key closes the race where two first requests both observe no
        // mapping. Both calls resolve to the same provider customer, and the losing DB insert can then
        // safely return the mapping written by the winner.
        var idempotencyKey = $"customer-binding:{productId}:{userId:N}";
        var customerReference = await paymentGateway.CreateCustomerAsync(
            productId, userId, email, idempotencyKey, ct);

        var inserted = await repository.TryAddMappingAsync(
            new CustomerMapping(productId, userId, customerReference), ct);

        if (!inserted)
        {
            var winner = await repository.GetMappingByUserIdAsync(productId, userId, ct);
            if (winner is not null)
            {
                return Result<string>.Success(winner.CustomerReference);
            }

            throw new InvalidOperationException("Customer mapping conflict was not followed by a readable mapping.");
        }

        return Result<string>.Success(customerReference);
    }

    public async Task<string> RecreateCustomerAsync(
        string productId, Guid userId, string email, CancellationToken ct = default)
    {
        // Re-creation intentionally gets a fresh key; reusing the initial key could replay the deleted
        // customer's original Stripe response.
        var customerReference = await paymentGateway.CreateCustomerAsync(
            productId, userId, email, $"customer-replacement:{Guid.NewGuid():N}", ct);

        await repository.UpdateMappingCustomerReferenceAsync(productId, userId, customerReference, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return customerReference;
    }
}
