# Payments.Gateway

Standalone multi-product payment gateway extracted from the DopamineKick monolith's Payments module.
Owns all Stripe state for one Stripe account and bills multiple products off distinct Prices; each
product keeps its own local entitlement read-model, fed by the `SubscriptionEntitlementChanged` event
this service publishes. See `docs/PAYMENT_GATEWAY_EXTRACTION_PLAN.md` in the DopamineKick.API repo for
the full design.

## Layout

```
src/
  Shared/                 vendored building blocks (copied from the monolith's Common.*)
    Common.Abstractions/    Result, IEndpointDefinition, IAuditable, messaging contracts
    Common.Infrastructure/  AuditableEntityInterceptor, RabbitMQ publisher
    Common.IntegrationEvents/ SubscriptionEntitlementChanged (the cross-service wire contract)
  Payments.Domain/        entities (with ProductId), SubscriptionStatus + tokens/access rule
  Payments.Application/    commands/queries, ports, product registry abstraction
  Payments.Infrastructure/ EF, StripePaymentGateway (ACL), sync service, inbox + outbox pollers
  Payments.Api/           product-scoped endpoints + DI module
  Host/                    ASP.NET Core host (JWT auth, migrate-on-boot, RabbitMQ)
```

## Multi-product

Every record is scoped by a `productId` slug (e.g. `dopamine-kick`). Per-product Price / trial / redirect
URLs come from the `Products` config section; the global Stripe secret + webhook secret are in `Stripe`.
User-facing endpoints are `api/billing/{productId}/...`; the single `api/billing/webhook` resolves the
product from the customer mapping at sync time (webhook payloads are never trusted for routing).

## Configuration

- `ConnectionStrings:PaymentsDBConnectionString` — Postgres.
- `Stripe:{SecretKey,PublishableKey,WebhookSecret}` — single account.
- `Products:<slug>:{PriceId,TrialPeriodDays,SuccessUrl,CancelUrl,PortalReturnUrl}` — one per product.
- `RabbitMQ:*` — publishes to the `payments-direct` exchange.
- `Jwt:{Authority,Audience}` — shared Keycloak realm (same issuer as the monolith).
