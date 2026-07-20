using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gateway.IntegrationTests.Infrastructure;

/// <summary>
/// Replaces the real Keycloak-backed JWT bearer scheme in tests. Every request is treated as
/// authenticated; the user id comes from the <c>X-Test-UserId</c> header (a GUID) so a test can act as
/// an arbitrary user. This satisfies <c>RequireAuthorization()</c> on the billing endpoints, whose
/// handlers read <see cref="ClaimTypes.NameIdentifier"/> and parse it as a Guid.
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserIdHeader = "X-Test-UserId";

    /// <summary>When present, the request is treated as anonymous so the endpoint challenges with 401.</summary>
    public const string NoAuthHeader = "X-Test-NoAuth";

    /// <summary>Default user id used when a request does not set <see cref="UserIdHeader"/>.</summary>
    public static readonly Guid DefaultUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey(NoAuthHeader))
        {
            // No ticket -> RequireAuthorization() challenges -> 401.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var subject = Request.Headers.TryGetValue(UserIdHeader, out var header) && !string.IsNullOrEmpty(header)
            ? header.ToString()
            : DefaultUserId.ToString();

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, subject) };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
