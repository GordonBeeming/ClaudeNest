using System.Security.Claims;
using System.Text.Encodings.Web;
using ClaudeNest.Backend.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ClaudeNest.Backend.Auth;

/// <summary>
/// Development-only auth handler that auto-authenticates as the seeded dev user.
/// Allows API endpoints with [Authorize] to work without Auth0 in local dev.
/// </summary>
public class DevAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevAuth";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("sub", DevDataSeeder.DevAuth0UserId),
            new Claim(ClaimTypes.Email, "dev@localhost"),
            new Claim(ClaimTypes.Name, "Local Developer")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
