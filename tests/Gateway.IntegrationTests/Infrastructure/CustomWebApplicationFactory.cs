using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real gateway host against the containers from <see cref="ContainerFixture"/>. Only the
/// external dependency — Keycloak/JWT auth — is swapped for <see cref="TestAuthHandler"/>. Postgres,
/// RabbitMQ, the inbox poller and the outbox publisher all run for real so the event flows are exercised.
///
/// Config is injected via environment variables rather than ConfigureAppConfiguration because the host's
/// RegisterServices reads connection strings, the product registry and JWT settings during Program.Main —
/// before the factory's config callbacks are layered in. Environment variables are read by
/// WebApplication.CreateBuilder up front, so they are visible at registration time.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>The single product registered for the test host.</summary>
    public const string ProductId = "dopamine-kick";

    private readonly Dictionary<string, string?> _originalEnv = new();
    private readonly Dictionary<string, string?> _values;

    public CustomWebApplicationFactory(ContainerFixture containers)
    {
        _values = new Dictionary<string, string?>
        {
            ["ConnectionStrings__PaymentsDBConnectionString"] = containers.ConnectionString,

            ["RabbitMQ__HostName"] = containers.RabbitMqHost,
            ["RabbitMQ__Port"] = containers.RabbitMqPort.ToString(),
            ["RabbitMQ__UserName"] = containers.RabbitMqUserName,
            ["RabbitMQ__Password"] = containers.RabbitMqPassword,
            ["RabbitMQ__VirtualHost"] = "/",

            // Present only so registration doesn't throw; auth is replaced so these are never contacted.
            ["Jwt__Authority"] = "https://test-authority.local/realms/test",
            ["Jwt__Audience"] = "account",

            // Dummy Stripe config so the Stripe client can be constructed; no real Stripe calls are made in
            // tests (endpoints that reach Stripe aren't exercised; the webhook uses a bad signature).
            ["Stripe__SecretKey"] = "sk_test_dummy",
            ["Stripe__PublishableKey"] = "pk_test_dummy",
            ["Stripe__WebhookSecret"] = "whsec_dummy",

            // Register the one product so IProductBillingRegistry.IsKnown("dopamine-kick") is true.
            [$"Products__{ProductId}__PriceId"] = "price_dummy",
            [$"Products__{ProductId}__TrialPeriodDays"] = "14",
            [$"Products__{ProductId}__SuccessUrl"] = "http://localhost/success",
            [$"Products__{ProductId}__CancelUrl"] = "http://localhost/cancel",
            [$"Products__{ProductId}__PortalReturnUrl"] = "http://localhost/account",
        };

        foreach (var (key, value) in _values)
        {
            _originalEnv[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" avoids loading appsettings.Development.json.
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Make the test scheme the default so RequireAuthorization() authenticates against it
            // instead of the real JWT bearer handler.
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            foreach (var (key, value) in _originalEnv)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
