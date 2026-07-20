using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Gateway.IntegrationTests.Infrastructure;

/// <summary>
/// Spins up the real infrastructure the gateway needs to boot — Postgres and RabbitMQ — once for the
/// whole test run and exposes connection settings for <see cref="CustomWebApplicationFactory"/>. The
/// gateway uses a single database, so no extra databases are created. Keycloak is external and is faked
/// at the auth layer instead (see <see cref="TestAuthHandler"/>).
/// </summary>
public sealed class ContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithDatabase("paymentsgatewaydb")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public string RabbitMqHost { get; private set; } = string.Empty;
    public int RabbitMqPort { get; private set; }
    public string RabbitMqUserName => "rabbitmq"; // Testcontainers RabbitMq default user
    public string RabbitMqPassword => "rabbitmq"; // Testcontainers RabbitMq default password

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _postgres.StartAsync(),
            _rabbitMq.StartAsync());

        ConnectionString = _postgres.GetConnectionString();
        RabbitMqHost = _rabbitMq.Hostname;
        RabbitMqPort = _rabbitMq.GetMappedPublicPort(5672);
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _rabbitMq.DisposeAsync().AsTask());
    }
}
