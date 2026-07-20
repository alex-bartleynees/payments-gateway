using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gateway.IntegrationTests.Infrastructure;

/// <summary>
/// Shared across the whole integration collection: owns the containers and the bootstrapped host.
/// Building the host triggers the gateway's start-up migration, so the schema exists by the time any
/// test runs.
/// </summary>
public sealed class ApiTestFixture : IAsyncLifetime
{
    private readonly ContainerFixture _containers = new();
    private CustomWebApplicationFactory? _factory;

    public CustomWebApplicationFactory Factory =>
        _factory ?? throw new InvalidOperationException("Fixture not initialized");

    public async Task InitializeAsync()
    {
        await _containers.InitializeAsync();
        _factory = new CustomWebApplicationFactory(_containers);

        // Force host construction (and therefore the start-up migration) before the first test.
        _ = _factory.Services.GetRequiredService<IServiceProvider>();
    }

    /// <summary>An HttpClient authenticated as <paramref name="userId"/> (default fixed test user).</summary>
    public HttpClient CreateClientAs(Guid? userId = null)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.UserIdHeader,
            (userId ?? TestAuthHandler.DefaultUserId).ToString());
        return client;
    }

    /// <summary>An HttpClient whose requests are treated as anonymous (expect 401 on secured endpoints).</summary>
    public HttpClient CreateAnonymousClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.NoAuthHeader, "true");
        return client;
    }

    /// <summary>Runs an action against a fresh scoped service provider (e.g. to read/write a DbContext).</summary>
    public async Task WithScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        await action(scope.ServiceProvider);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _containers.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<ApiTestFixture>
{
    public const string Name = "Integration";
}
