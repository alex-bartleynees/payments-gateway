using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payments.Domain.Entities;

namespace Payments.Infrastructure.Configuration;

public class CustomerMappingConfiguration : IEntityTypeConfiguration<CustomerMapping>
{
    public void Configure(EntityTypeBuilder<CustomerMapping> builder)
    {
        // Composite key: the same user can subscribe to multiple products (distinct provider customers).
        builder.HasKey(x => new { x.ProductId, x.UserId });

        builder.Property(x => x.ProductId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CustomerReference)
            .IsRequired()
            .HasMaxLength(255);

        // Provider customer ids are globally unique within the one Stripe account.
        builder.HasIndex(x => x.CustomerReference)
            .IsUnique();
    }
}
