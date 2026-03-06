using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeNest.Backend.IntegrationTests.Infrastructure;

namespace ClaudeNest.Backend.IntegrationTests.Controllers;

public class AgentDownloadControllerTests(ClaudeNestWebApplicationFactory factory) : IClassFixture<ClaudeNestWebApplicationFactory>
{
    [Fact]
    public async Task GetAvailability_ReturnsAvailability()
    {
        var user = new TestUser("auth0|dl-avail", "dl-avail@test.com", "DL Avail");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/agent-download/available");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("available").GetBoolean());
    }

    [Fact]
    public async Task GetAvailability_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = factory.CreateAuthenticatedClient(TestUsers.Anonymous);

        var response = await client.GetAsync("/api/agent-download/available");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DownloadAgent_ReturnsBadRequest_ForInvalidRid()
    {
        var user = new TestUser("auth0|dl-badrid", "dl-badrid@test.com", "DL BadRid");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/agent-download/invalid-rid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DownloadAgent_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = factory.CreateAuthenticatedClient(TestUsers.Anonymous);

        var response = await client.GetAsync("/api/agent-download/linux-x64");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
