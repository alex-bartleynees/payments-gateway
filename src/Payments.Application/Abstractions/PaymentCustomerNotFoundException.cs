namespace Payments.Application.Abstractions;

/// <summary>
/// Thrown by <see cref="IPaymentGateway"/> when a customer reference that exists in our own mapping
/// is unknown to the provider (e.g. deleted directly in the provider dashboard). Provider-agnostic by
/// design, matching the <see cref="IPaymentGateway"/> ACL boundary — callers never see the underlying
/// provider SDK's exception type.
/// </summary>
public class PaymentCustomerNotFoundException(string customerReference)
    : Exception($"Customer '{customerReference}' was not found with the payment provider.")
{
    public string CustomerReference { get; } = customerReference;
}
