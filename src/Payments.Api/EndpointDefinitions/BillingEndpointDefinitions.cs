using System.Security.Claims;
using SharedKernel.AspNetCore;
using SharedKernel.Abstractions;
using SharedKernel.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Payments.Application.Abstractions;
using Payments.Application.Common.Models;
using Payments.Application.Payments.Commands;
using Payments.Application.Payments.Queries;
using Payments.Domain.Errors;

namespace Payments.Api.EndpointDefinitions;

/// <summary>
/// Product-scoped billing endpoints. The <c>{productId}</c> route segment selects which product's
/// billing configuration applies and is validated against the product registry up-front, so an unknown
/// product is rejected before any provider call. The authenticated user comes from the JWT.
/// </summary>
public class BillingEndpointDefinitions : IEndpointDefinition
{
    public void RegisterEndpoints(WebApplication app)
    {
        var billing = app.MapGroup("api/billing/{productId}").RequireAuthorization();

        billing.MapPost("customer", EnsureCustomer);
        billing.MapPost("checkout", CreateCheckout);
        billing.MapPost("portal", CreatePortal);
        billing.MapPost("sync", Sync);
        billing.MapGet("subscription", GetSubscription);
    }

    private static async Task<Results<Ok<CustomerResponse>, ProblemHttpResult>> EnsureCustomer(
        string productId, IProductBillingRegistry registry, Payments.Api.Mediator.Mediator mediator, ClaimsPrincipal user)
    {
        if (!registry.IsKnown(productId))
        {
            return PaymentsErrors.UnknownProduct(productId).ToProblem();
        }

        if (user.GetUserId() is not { } userId)
        {
            return PaymentsErrors.MissingUserId.ToProblem();
        }

        var result = await mediator.Send(new EnsureCustomer(productId, userId, GetEmail(user)));
        return result.IsSuccess
            ? TypedResults.Ok(new CustomerResponse(result.ValueOrThrow))
            : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<CheckoutSessionResponse>, ProblemHttpResult>> CreateCheckout(
        string productId, IProductBillingRegistry registry, Payments.Api.Mediator.Mediator mediator, ClaimsPrincipal user)
    {
        if (!registry.IsKnown(productId))
        {
            return PaymentsErrors.UnknownProduct(productId).ToProblem();
        }

        if (user.GetUserId() is not { } userId)
        {
            return PaymentsErrors.MissingUserId.ToProblem();
        }

        var result = await mediator.Send(new CreateCheckoutSession(productId, userId, GetEmail(user)));
        return result.IsSuccess ? TypedResults.Ok(result.ValueOrThrow) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<PortalSessionResponse>, ProblemHttpResult>> CreatePortal(
        string productId, IProductBillingRegistry registry, Payments.Api.Mediator.Mediator mediator, ClaimsPrincipal user)
    {
        if (!registry.IsKnown(productId))
        {
            return PaymentsErrors.UnknownProduct(productId).ToProblem();
        }

        if (user.GetUserId() is not { } userId)
        {
            return PaymentsErrors.MissingUserId.ToProblem();
        }

        var result = await mediator.Send(new CreatePortalSession(productId, userId));
        return result.IsSuccess ? TypedResults.Ok(result.ValueOrThrow) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok, ProblemHttpResult>> Sync(
        string productId, IProductBillingRegistry registry, Payments.Api.Mediator.Mediator mediator, ClaimsPrincipal user)
    {
        if (!registry.IsKnown(productId))
        {
            return PaymentsErrors.UnknownProduct(productId).ToProblem();
        }

        if (user.GetUserId() is not { } userId)
        {
            return PaymentsErrors.MissingUserId.ToProblem();
        }

        var result = await mediator.Send(new SyncSubscription(productId, userId));
        return result.IsSuccess ? TypedResults.Ok() : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<SubscriptionStateDto>, ProblemHttpResult>> GetSubscription(
        string productId, IProductBillingRegistry registry, Payments.Api.Mediator.Mediator mediator, ClaimsPrincipal user)
    {
        if (!registry.IsKnown(productId))
        {
            return PaymentsErrors.UnknownProduct(productId).ToProblem();
        }

        if (user.GetUserId() is not { } userId)
        {
            return PaymentsErrors.MissingUserId.ToProblem();
        }

        var dto = await mediator.Send(new GetSubscriptionState(productId, userId));
        return TypedResults.Ok(dto);
    }

    private static string GetEmail(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value ?? string.Empty;
}

// The customer reference is an opaque provider id — named after the concept, not the vendor, on the
// wire too. Serializes as camelCase `customerReference`.
public record CustomerResponse(string CustomerReference);

internal static class BillingErrorExtensions
{
    public static ProblemHttpResult ToProblem(this Error error) =>
        TypedResults.Problem(detail: error.Detail, statusCode: error.Status, title: error.Title);
}
