using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payments.Domain.Billing;
using Payments.Domain.Entities;

namespace Payments.Infrastructure.Configuration;

public class SubscriptionStateConfiguration : IEntityTypeConfiguration<SubscriptionState>
{
    public void Configure(EntityTypeBuilder<SubscriptionState> builder)
    {
        builder.HasKey(x => x.CustomerReference);

        builder.Property(x => x.CustomerReference)
            .HasMaxLength(255);

        builder.Property(x => x.ProductId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.SubscriptionReference)
            .HasMaxLength(255);

        // Persist the domain enum as its stable lower-case token (e.g. "past_due"), so the column stays
        // human-readable and existing rows round-trip unchanged. Same token map used by the ACL and JSON.
        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion(
                status => status.ToToken(),
                token => SubscriptionStatusTokens.FromToken(token));

        builder.Property(x => x.PriceId)
            .HasMaxLength(255);

        builder.Property(x => x.PaymentMethodBrand)
            .HasMaxLength(50);

        builder.Property(x => x.PaymentMethodLast4)
            .HasMaxLength(4);

        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.Status);
    }
}
