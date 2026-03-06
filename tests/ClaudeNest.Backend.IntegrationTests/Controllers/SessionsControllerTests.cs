using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeNest.Backend.IntegrationTests.Infrastructure;

namespace ClaudeNest.Backend.IntegrationTests.Controllers;

public class SessionsControllerTests(ClaudeNestWebApplicationFactory factory) : IClassFixture<ClaudeNestWebApplicationFactory>
{
    [Fact]
    public async Task GetSessions_ReturnsSessions()
    {
        var user = new TestUser("auth0|sessions-get", "sessions-get@test.com", "Sessions Get");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "Sessions Agent");
        await TestDatabaseHelper.SeedSessionAsync(factory.Services, agent.Id);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync($"/api/sessions/agent/{agent.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sessions = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(sessions.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task GetSessions_ReturnsEmptyForNoSessions()
    {
        var user = new TestUser("auth0|sessions-empty", "sessions-empty@test.com", "Sessions Empty");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "Empty Sessions Agent");

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync($"/api/sessions/agent/{agent.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sessions = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, sessions.GetArrayLength());
    }

    [Fact]
    public async Task GetSessions_Returns404ForOtherUsersAgent()
    {
        var ownerUser = new TestUser("auth0|sessions-owner", "sessions-owner@test.com", "Sessions Owner");
        var (_, ownerAccount) = await TestDatabaseHelper.SeedUserAsync(factory.Services, ownerUser);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, ownerAccount.Id, "Other Sessions Agent");

        var viewerUser = new TestUser("auth0|sessions-viewer", "sessions-viewer@test.com", "Sessions Viewer");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, viewerUser);

        var client = factory.CreateAuthenticatedClient(viewerUser);

        var response = await client.GetAsync($"/api/sessions/agent/{agent.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSessions_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = factory.CreateAuthenticatedClient(TestUsers.Anonymous);

        var response = await client.GetAsync($"/api/sessions/agent/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
