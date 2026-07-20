namespace Payments.Application.Abstractions;

public interface IPaymentsUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
