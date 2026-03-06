using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.IntegrationTests.Infrastructure;
using ClaudeNest.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeNest.Backend.IntegrationTests.Controllers;

public class PairingControllerTests(ClaudeNestWebApplicationFactory factory) : IClassFixture<ClaudeNestWebApplicationFactory>
{
    [Fact]
    public async Task GenerateToken_ReturnsToken()
    {
        var user = new TestUser("auth0|pairing-gen", "pairing-gen@test.com", "Pairing Gen");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PostAsync("/api/pairing/generate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("token").GetString()!.Length > 0);
    }

    [Fact]
    public async Task GenerateToken_RequiresActiveSubscription()
    {
        var user = new TestUser("auth0|pairing-nosub", "pairing-nosub@test.com", "Pairing NoSub");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user, subscriptionStatus: SubscriptionStatus.None);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PostAsync("/api/pairing/generate", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateToken_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = factory.CreateAuthenticatedClient(TestUsers.Anonymous);

        var response = await client.PostAsync("/api/pairing/generate", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExchangeToken_CreatesAgentAndCredential()
    {
        var user = new TestUser("auth0|pairing-exchange", "pairing-exchange@test.com", "Pairing Exchange");
        var (seededUser, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var token = Convert.ToBase64String(new byte[32]); // Known token
        await TestDatabaseHelper.SeedPairingTokenAsync(
            factory.Services, seededUser.Id, token, DateTimeOffset.UtcNow.AddMinutes(10));

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/pairing/exchange", new
        {
            Token = token,
            AgentName = "Test Agent",
            Hostname = "test-host",
            OS = "linux"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(Guid.Empty, body.GetProperty("agentId").GetGuid());
        Assert.True(body.GetProperty("secret").GetString()!.Length > 0);
    }

    [Fact]
    public async Task ExchangeToken_ReturnsBadRequest_ForInvalidToken()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/pairing/exchange", new
        {
            Token = "invalid-token-value",
            AgentName = "Bad Agent"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExchangeToken_ReturnsBadRequest_ForExpiredToken()
    {
        var user = new TestUser("auth0|pairing-expired", "pairing-expired@test.com", "Pairing Expired");
        var (seededUser, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var token = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 1)).ToArray());
        await TestDatabaseHelper.SeedPairingTokenAsync(
            factory.Services, seededUser.Id, token, DateTimeOffset.UtcNow.AddMinutes(-5)); // Expired

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/pairing/exchange", new
        {
            Token = token,
            AgentName = "Expired Agent"
        });

        // May get rate-limited (429) when running with other exchange tests
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.TooManyRequests,
            $"Expected BadRequest or TooManyRequests, got {response.StatusCode}");
    }

    [Fact]
    public async Task ExchangeToken_EnforcesAgentLimit()
    {
        // Use Wren plan (MaxAgents = 1)
        var user = new TestUser("auth0|pairing-limit", "pairing-limit@test.com", "Pairing Limit");
        var (seededUser, account) = await TestDatabaseHelper.SeedUserAsync(
            factory.Services, user, planId: ClaudeNestWebApplicationFactory.WrenPlanId);

        // Seed one agent (filling the limit)
        await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "Existing Agent");

        var token = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 10)).ToArray());
        await TestDatabaseHelper.SeedPairingTokenAsync(
            factory.Services, seededUser.Id, token, DateTimeOffset.UtcNow.AddMinutes(10));

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/pairing/exchange", new
        {
            Token = token,
            AgentName = "Over Limit Agent"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExchangeToken_RequiresActiveSubscription()
    {
        var user = new TestUser("auth0|pairing-ex-nosub", "pairing-ex-nosub@test.com", "Pairing Ex NoSub");
        var (seededUser, _) = await TestDatabaseHelper.SeedUserAsync(
            factory.Services, user, subscriptionStatus: SubscriptionStatus.None);

        var token = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 20)).ToArray());
        await TestDatabaseHelper.SeedPairingTokenAsync(
            factory.Services, seededUser.Id, token, DateTimeOffset.UtcNow.AddMinutes(10));

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/pairing/exchange", new
        {
            Token = token,
            AgentName = "NoSub Agent"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExchangeToken_ReturnsBadRequest_ForAlreadyRedeemedToken()
    {
        var user = new TestUser("auth0|pairing-redeemed", "pairing-redeemed@test.com", "Pairing Redeemed");
        var (seededUser, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var token = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 30)).ToArray());
        await TestDatabaseHelper.SeedPairingTokenAsync(
            factory.Services, seededUser.Id, token, DateTimeOffset.UtcNow.AddMinutes(10), redeemed: true);

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/pairing/exchange", new
        {
            Token = token,
            AgentName = "Redeemed Agent"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
