using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeNest.Backend.IntegrationTests.Infrastructure;

namespace ClaudeNest.Backend.IntegrationTests.Controllers;

public class MeControllerTests(ClaudeNestWebApplicationFactory factory) : IClassFixture<ClaudeNestWebApplicationFactory>
{
    [Fact]
    public async Task GetMe_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = factory.CreateAuthenticatedClient(TestUsers.Anonymous);

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_AutoProvisionsUser_OnFirstLogin()
    {
        var newUser = new TestUser("auth0|me-autoprovision", "me-autoprovision@test.com", "Auto Provision");
        var client = factory.CreateAuthenticatedClient(newUser);

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("me-autoprovision@test.com", body.GetProperty("email").GetString());
        Assert.NotEqual(Guid.Empty, body.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task GetMe_AdminGetsHighestPlan()
    {
        var adminUser = new TestUser("auth0|me-admin-plan", "me-admin-plan@test.com", "Admin Plan");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        var client = factory.CreateAuthenticatedClient(adminUser);

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Condor", body.GetProperty("account").GetProperty("planName").GetString());
    }

    [Fact]
    public async Task GetMe_CompanyDealAssignsCorrectPlan()
    {
        // Seed admin to create the deal
        var adminUser = new TestUser("auth0|me-deal-admin", "me-deal-admin@test.com", "Deal Admin");
        var (admin, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        await TestDatabaseHelper.SeedCompanyDealAsync(
            factory.Services, admin.Id, ClaudeNestWebApplicationFactory.EaglePlanId, "medeal.com");

        // New user with matching domain
        var companyUser = new TestUser("auth0|me-deal-user", "employee@medeal.com", "Employee");
        var client = factory.CreateAuthenticatedClient(companyUser);

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Eagle", body.GetProperty("account").GetProperty("planName").GetString());
        Assert.Equal("Active", body.GetProperty("account").GetProperty("subscriptionStatus").GetString());
    }

    [Fact]
    public async Task GetMe_ReturnsExistingUser()
    {
        var existingUser = new TestUser("auth0|me-existing", "me-existing@test.com", "Existing User");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, existingUser);

        var client = factory.CreateAuthenticatedClient(existingUser);

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("me-existing@test.com", body.GetProperty("email").GetString());
    }
}
