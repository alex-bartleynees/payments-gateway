using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payments.Domain.Entities;

namespace Payments.Infrastructure.Configuration;

public class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductId)
            .HasMaxLength(100);

        builder.Property(x => x.EventReference)
            .IsRequired()
            .HasMaxLength(255);

        // Dedup key: a redelivered webhook with the same event reference must not create a second row.
        builder.HasIndex(x => x.EventReference).IsUnique();

        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CustomerReference)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.LastError)
            .HasMaxLength(500);

        // Drives the poller's claim query (WHERE Processed = false ... ORDER BY CreatedAt).
        builder.HasIndex(x => new { x.Processed, x.CreatedAt });
    }
}
