using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaudeNest.Backend.IntegrationTests.Infrastructure;

public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestAuth";
    public const string UserIdHeader = "X-Test-UserId";
    public const string EmailHeader = "X-Test-Email";
    public const string NameHeader = "X-Test-Name";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userIdValues) ||
            string.IsNullOrEmpty(userIdValues.FirstOrDefault()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = userIdValues.First()!;
        var email = Request.Headers.TryGetValue(EmailHeader, out var emailValues)
            ? emailValues.FirstOrDefault()
            : null;
        var name = Request.Headers.TryGetValue(NameHeader, out var nameValues)
            ? nameValues.FirstOrDefault()
            : null;

        var claims = new List<Claim>
        {
            new("sub", userId)
        };

        if (email is not null)
            claims.Add(new("email", email));
        if (name is not null)
            claims.Add(new("name", name));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
