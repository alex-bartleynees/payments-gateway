using System.Text.Json.Serialization;
using SharedKernel.AspNetCore;
using SharedKernel.Messaging.Abstractions;
using SharedKernel.Messaging.RabbitMq;
using Microsoft.OpenApi;
using Payments.Api;
using Payments.Domain.Billing;

namespace Host.Extensions;

public static class GatewayExtensions
{
    public static void RegisterServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddPaymentsModule(builder.Configuration);

        // Register RabbitMQ options + publisher (the outbox publisher drains to this).
        builder.Services.Configure<RabbitMqOptions>(
            builder.Configuration.GetSection(RabbitMqOptions.SectionName));
        builder.Services.AddSingleton<IRabbitMqConnectionFactory, RabbitMqConnectionFactory>();
        builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            // Registered before the generic converter: STJ uses the first matching converter in the
            // collection, so SubscriptionStatus serializes to the billing contract's lower-case tokens
            // (e.g. "past_due") rather than the enum member name.
            options.SerializerOptions.Converters.Add(new SubscriptionStatusJsonConverter());
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        builder.Services.AddProblemDetails();

        // Shared Keycloak realm — same issuer/audience as the DopamineKick monolith.
        builder.Services.AddAuthentication()
            .AddJwtBearer(options =>
            {
                options.Authority = builder.Configuration["Jwt:Authority"]
                    ?? throw new ArgumentNullException("Jwt:Authority", "JWT Authority must be configured");
                options.Audience = builder.Configuration["Jwt:Audience"]
                    ?? throw new ArgumentNullException("Jwt:Audience", "JWT Audience must be configured");
            });

        builder.Services.AddAuthorizationBuilder();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = "Payments Gateway",
                Description = "Multi-product payment gateway API",
            });
        });

        builder.Services.AddHealthChecks();
    }

    public static void RegisterAppConfig(this WebApplication app)
    {
        app.Services.MigratePaymentsDatabase();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthChecks("/health");

        app.RegisterEndpointDefinitions();
    }

    private static void RegisterEndpointDefinitions(this WebApplication app)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.EndsWith(".Api") == true);

        var endpointDefinitions = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsAssignableTo(typeof(IEndpointDefinition)) && !t.IsAbstract && !t.IsInterface)
            .Select(Activator.CreateInstance)
            .Cast<IEndpointDefinition>();

        foreach (var endpointDefinition in endpointDefinitions)
        {
            endpointDefinition.RegisterEndpoints(app);
        }
    }
}
