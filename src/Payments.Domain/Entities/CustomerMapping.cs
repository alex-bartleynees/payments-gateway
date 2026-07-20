using SharedKernel.Abstractions;

namespace Payments.Domain.Entities;

/// <summary>
/// Eager binding between a (<see cref="ProductId"/>, <see cref="UserId"/>) pair and the payment
/// provider customer created for them. Created before any Checkout Session exists so Checkout never
/// implicitly creates a second customer for the same user. The same person subscribing to two
/// products gets two distinct provider customers (one row per product), so the key is composite.
/// </summary>
public class CustomerMapping : IAuditable
{
    /// <summary>Product slug this binding belongs to (e.g. <c>dopamine-kick</c>). Part of the composite key.</summary>
    public string ProductId { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public string CustomerReference { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public CustomerMapping() { }

    public CustomerMapping(string productId, Guid userId, string customerReference)
    {
        ProductId = productId;
        UserId = userId;
        CustomerReference = customerReference;
    }
}
