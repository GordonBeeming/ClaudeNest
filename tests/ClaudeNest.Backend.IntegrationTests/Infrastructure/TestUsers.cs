namespace ClaudeNest.Backend.IntegrationTests.Infrastructure;

public static class TestUsers
{
    public static readonly TestUser UserA = new("auth0|user-a", "usera@test.com", "User A");
    public static readonly TestUser UserB = new("auth0|user-b", "userb@test.com", "User B");
    public static readonly TestUser AdminUser = new("auth0|admin", "admin@test.com", "Admin User");
    public static readonly TestUser CompanyUser = new("auth0|company", "user@acme.com", "Company User");
    public static readonly TestUser Anonymous = new(null, null, null);
}

public record TestUser(string? Auth0UserId, string? Email, string? Name)
{
    public bool IsAuthenticated => Auth0UserId is not null;
}
