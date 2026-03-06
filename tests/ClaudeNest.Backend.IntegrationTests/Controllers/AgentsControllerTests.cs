using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeNest.Backend.IntegrationTests.Controllers;

public class AgentsControllerTests(ClaudeNestWebApplicationFactory factory) : IClassFixture<ClaudeNestWebApplicationFactory>
{
    [Fact]
    public async Task GetAgents_ReturnsOwnAgents()
    {
        var user = new TestUser("auth0|agents-own", "agents-own@test.com", "Agent Owner");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "My Agent");

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/agents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var agents = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(agents.GetArrayLength() >= 1);
        Assert.Contains("My Agent", agents.EnumerateArray().Select(a => a.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task GetAgents_DoesNotReturnOtherUsersAgents()
    {
        var ownerUser = new TestUser("auth0|agents-other-owner", "agents-other-owner@test.com", "Other Owner");
        var (_, ownerAccount) = await TestDatabaseHelper.SeedUserAsync(factory.Services, ownerUser);
        await TestDatabaseHelper.SeedAgentAsync(factory.Services, ownerAccount.Id, "Other Agent");

        var viewerUser = new TestUser("auth0|agents-viewer", "agents-viewer@test.com", "Viewer");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, viewerUser);

        var client = factory.CreateAuthenticatedClient(viewerUser);

        var response = await client.GetAsync("/api/agents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var agents = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain("Other Agent", agents.EnumerateArray().Select(a => a.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task GetAgent_ReturnsAgentById()
    {
        var user = new TestUser("auth0|agents-byid", "agents-byid@test.com", "Agent ById");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "ById Agent");

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync($"/api/agents/{agent.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ById Agent", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetAgent_Returns404ForNonexistentAgent()
    {
        var user = new TestUser("auth0|agents-404", "agents-404@test.com", "Agent 404");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync($"/api/agents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAgent_Returns404ForOtherUsersAgent()
    {
        var ownerUser = new TestUser("auth0|agents-other-get", "agents-other-get@test.com", "Other Get");
        var (_, ownerAccount) = await TestDatabaseHelper.SeedUserAsync(factory.Services, ownerUser);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, ownerAccount.Id, "Others Agent");

        var viewerUser = new TestUser("auth0|agents-get-viewer", "agents-get-viewer@test.com", "Get Viewer");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, viewerUser);

        var client = factory.CreateAuthenticatedClient(viewerUser);

        var response = await client.GetAsync($"/api/agents/{agent.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCredentials_ReturnsCredentialsForOwnAgent()
    {
        var user = new TestUser("auth0|agents-creds", "agents-creds@test.com", "Creds User");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "Creds Agent");
        await TestDatabaseHelper.SeedCredentialAsync(factory.Services, agent.Id);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync($"/api/agents/{agent.Id}/credentials");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var creds = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(creds.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task GetCredentials_Returns404ForOtherUsersAgent()
    {
        var ownerUser = new TestUser("auth0|agents-creds-owner", "agents-creds-owner@test.com", "Creds Owner");
        var (_, ownerAccount) = await TestDatabaseHelper.SeedUserAsync(factory.Services, ownerUser);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, ownerAccount.Id, "Creds Other");

        var viewerUser = new TestUser("auth0|agents-creds-viewer", "agents-creds-viewer@test.com", "Creds Viewer");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, viewerUser);

        var client = factory.CreateAuthenticatedClient(viewerUser);

        var response = await client.GetAsync($"/api/agents/{agent.Id}/credentials");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RotateSecret_CreatesNewCredential()
    {
        var user = new TestUser("auth0|agents-rotate", "agents-rotate@test.com", "Rotate User");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "Rotate Agent");

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PostAsync($"/api/agents/{agent.Id}/rotate-secret", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("secret").GetString()!.Length > 0);
        Assert.NotEqual(Guid.Empty, body.GetProperty("credentialId").GetGuid());

        // Verify database state
        var newCredentialId = body.GetProperty("credentialId").GetGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
        var dbCredential = await db.AgentCredentials.FirstOrDefaultAsync(c => c.Id == newCredentialId);
        Assert.NotNull(dbCredential);
        Assert.Equal(agent.Id, dbCredential.AgentId);
        Assert.Null(dbCredential.RevokedAt);
    }

    [Fact]
    public async Task RotateSecret_RevokesOldCredentials()
    {
        var user = new TestUser("auth0|agents-revoke-old", "agents-revoke-old@test.com", "Revoke Old");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "Revoke Agent");
        var oldCred = await TestDatabaseHelper.SeedCredentialAsync(factory.Services, agent.Id);

        var client = factory.CreateAuthenticatedClient(user);

        await client.PostAsync($"/api/agents/{agent.Id}/rotate-secret", null);

        // Verify old credential was revoked
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
        var cred = await db.AgentCredentials.FirstAsync(c => c.Id == oldCred.Id);
        Assert.NotNull(cred.RevokedAt);
    }

    [Fact]
    public async Task RotateSecret_Returns404ForOtherUsersAgent()
    {
        var ownerUser = new TestUser("auth0|agents-rotate-owner", "agents-rotate-owner@test.com", "Rotate Owner");
        var (_, ownerAccount) = await TestDatabaseHelper.SeedUserAsync(factory.Services, ownerUser);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, ownerAccount.Id, "Rotate Others");

        var otherUser = new TestUser("auth0|agents-rotate-other", "agents-rotate-other@test.com", "Rotate Other");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, otherUser);

        var client = factory.CreateAuthenticatedClient(otherUser);

        var response = await client.PostAsync($"/api/agents/{agent.Id}/rotate-secret", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAgent_RemovesAgent()
    {
        var user = new TestUser("auth0|agents-delete", "agents-delete@test.com", "Delete User");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "Delete Agent");

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.DeleteAsync($"/api/agents/{agent.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify agent is gone
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
        Assert.False(await db.Agents.AnyAsync(a => a.Id == agent.Id));
    }

    [Fact]
    public async Task DeleteAgent_Returns404ForOtherUsersAgent()
    {
        var ownerUser = new TestUser("auth0|agents-delete-owner", "agents-delete-owner@test.com", "Delete Owner");
        var (_, ownerAccount) = await TestDatabaseHelper.SeedUserAsync(factory.Services, ownerUser);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, ownerAccount.Id, "Delete Others");

        var otherUser = new TestUser("auth0|agents-delete-other", "agents-delete-other@test.com", "Delete Other");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, otherUser);

        var client = factory.CreateAuthenticatedClient(otherUser);

        var response = await client.DeleteAsync($"/api/agents/{agent.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Verify the agent was NOT deleted (ownership check prevented it)
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
        var dbAgent = await db.Agents.FirstOrDefaultAsync(a => a.Id == agent.Id);
        Assert.NotNull(dbAgent);
    }

    [Fact]
    public async Task DeleteAgent_RemovesRelatedData()
    {
        var user = new TestUser("auth0|agents-del-related", "agents-del-related@test.com", "Del Related");
        var (seededUser, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "Related Agent");
        await TestDatabaseHelper.SeedCredentialAsync(factory.Services, agent.Id);
        await TestDatabaseHelper.SeedSessionAsync(factory.Services, agent.Id);
        await TestDatabaseHelper.SeedFolderPreferenceAsync(factory.Services, seededUser.Id, agent.Id);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.DeleteAsync($"/api/agents/{agent.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
        Assert.False(await db.AgentCredentials.AnyAsync(c => c.AgentId == agent.Id));
        Assert.False(await db.Sessions.AnyAsync(s => s.AgentId == agent.Id));
        Assert.False(await db.UserFolderPreferences.AnyAsync(p => p.AgentId == agent.Id));
    }

    [Fact]
    public async Task RevokeCredential_RevokesSpecificCredential()
    {
        var user = new TestUser("auth0|agents-revoke-cred", "agents-revoke-cred@test.com", "Revoke Cred");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "RevokeCred Agent");
        var cred = await TestDatabaseHelper.SeedCredentialAsync(factory.Services, agent.Id);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.DeleteAsync($"/api/agents/{agent.Id}/credentials/{cred.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
        var updatedCred = await db.AgentCredentials.FirstAsync(c => c.Id == cred.Id);
        Assert.NotNull(updatedCred.RevokedAt);
    }
}
