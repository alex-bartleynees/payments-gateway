using Microsoft.Extensions.Configuration;
using Payments.Application.Abstractions;

namespace Payments.Infrastructure.Configuration;

/// <summary>
/// Reads the <c>Products</c> configuration section — a map of <c>productId → ProductBillingConfig</c> —
/// once at construction. Product slugs are matched case-insensitively.
/// </summary>
public class ProductBillingRegistry : IProductBillingRegistry
{
    public const string SectionName = "Products";

    private readonly IReadOnlyDictionary<string, ProductBillingConfig> _products;

    public ProductBillingRegistry(IConfiguration configuration)
    {
        var bound = configuration.GetSection(SectionName).Get<Dictionary<string, ProductBillingConfig>>()
                    ?? new Dictionary<string, ProductBillingConfig>();

        _products = new Dictionary<string, ProductBillingConfig>(bound, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsKnown(string productId) =>
        !string.IsNullOrWhiteSpace(productId) && _products.ContainsKey(productId);

    public ProductBillingConfig Get(string productId) =>
        _products.TryGetValue(productId, out var config)
            ? config
            : throw new KeyNotFoundException($"No billing configuration registered for product '{productId}'.");
}
