using Microsoft.EntityFrameworkCore;
using Payments.Application.Abstractions;
using Payments.Domain.Entities;
using Payments.Infrastructure.Configuration;

namespace Payments.Infrastructure.DbContexts;

public class PaymentsContext(DbContextOptions<PaymentsContext> options)
    : DbContext(options), IPaymentsUnitOfWork
{
    public DbSet<CustomerMapping> CustomerMappings { get; set; }

    public DbSet<SubscriptionState> SubscriptionStates { get; set; }

    public DbSet<InboxMessage> InboxMessages { get; set; }

    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomerMappingConfiguration).Assembly);
    }
}
