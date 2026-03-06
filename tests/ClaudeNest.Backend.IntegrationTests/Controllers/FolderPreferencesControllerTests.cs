using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeNest.Backend.IntegrationTests.Controllers;

public class FolderPreferencesControllerTests(ClaudeNestWebApplicationFactory factory) : IClassFixture<ClaudeNestWebApplicationFactory>
{
    [Fact]
    public async Task GetPreferences_ReturnsPreferences()
    {
        var user = new TestUser("auth0|fp-get", "fp-get@test.com", "FP Get");
        var (seededUser, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "FP Agent");
        await TestDatabaseHelper.SeedFolderPreferenceAsync(factory.Services, seededUser.Id, agent.Id, "/my/path");

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync($"/api/agents/{agent.Id}/folder-preferences");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var prefs = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(prefs.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task GetPreferences_ReturnsEmptyForNoPreferences()
    {
        var user = new TestUser("auth0|fp-empty", "fp-empty@test.com", "FP Empty");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "FP Empty Agent");

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync($"/api/agents/{agent.Id}/folder-preferences");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var prefs = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, prefs.GetArrayLength());
    }

    [Fact]
    public async Task GetPreferences_Returns404ForOtherUsersAgent()
    {
        var ownerUser = new TestUser("auth0|fp-owner", "fp-owner@test.com", "FP Owner");
        var (_, ownerAccount) = await TestDatabaseHelper.SeedUserAsync(factory.Services, ownerUser);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, ownerAccount.Id, "FP Other Agent");

        var viewerUser = new TestUser("auth0|fp-viewer", "fp-viewer@test.com", "FP Viewer");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, viewerUser);

        var client = factory.CreateAuthenticatedClient(viewerUser);

        var response = await client.GetAsync($"/api/agents/{agent.Id}/folder-preferences");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpsertPreference_CreatesNewPreference()
    {
        var user = new TestUser("auth0|fp-create", "fp-create@test.com", "FP Create");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "FP Create Agent");

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PutAsJsonAsync($"/api/agents/{agent.Id}/folder-preferences", new
        {
            Path = "/new/path",
            IsFavorite = true,
            Color = "#ff5733"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("/new/path", body.GetProperty("path").GetString());
        Assert.True(body.GetProperty("isFavorite").GetBoolean());
        Assert.Equal("#ff5733", body.GetProperty("color").GetString());

        // Verify database state
        using var scope1 = factory.Services.CreateScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<NestDbContext>();
        var dbPref = await db1.UserFolderPreferences.FirstOrDefaultAsync(p => p.AgentId == agent.Id && p.Path == "/new/path");
        Assert.NotNull(dbPref);
        Assert.True(dbPref.IsFavorite);
        Assert.Equal("#ff5733", dbPref.Color);
    }

    [Fact]
    public async Task UpsertPreference_UpdatesExistingPreference()
    {
        var user = new TestUser("auth0|fp-update", "fp-update@test.com", "FP Update");
        var (seededUser, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "FP Update Agent");
        await TestDatabaseHelper.SeedFolderPreferenceAsync(factory.Services, seededUser.Id, agent.Id, "/update/path", isFavorite: false);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PutAsJsonAsync($"/api/agents/{agent.Id}/folder-preferences", new
        {
            Path = "/update/path",
            IsFavorite = true,
            Color = "#aabbcc"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isFavorite").GetBoolean());
        Assert.Equal("#aabbcc", body.GetProperty("color").GetString());

        // Verify database state
        using var scope2 = factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<NestDbContext>();
        var dbPref = await db2.UserFolderPreferences.FirstOrDefaultAsync(p => p.AgentId == agent.Id && p.Path == "/update/path");
        Assert.NotNull(dbPref);
        Assert.True(dbPref.IsFavorite);
        Assert.Equal("#aabbcc", dbPref.Color);
    }

    [Fact]
    public async Task UpsertPreference_ReturnsBadRequest_ForEmptyPath()
    {
        var user = new TestUser("auth0|fp-empty-path", "fp-empty-path@test.com", "FP EmptyPath");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "FP EmptyPath Agent");

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PutAsJsonAsync($"/api/agents/{agent.Id}/folder-preferences", new
        {
            Path = "",
            IsFavorite = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpsertPreference_ReturnsBadRequest_ForInvalidColor()
    {
        var user = new TestUser("auth0|fp-bad-color", "fp-bad-color@test.com", "FP BadColor");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "FP BadColor Agent");

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PutAsJsonAsync($"/api/agents/{agent.Id}/folder-preferences", new
        {
            Path = "/some/path",
            IsFavorite = true,
            Color = "not-a-color"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpsertPreference_AcceptsValidHexColor()
    {
        var user = new TestUser("auth0|fp-hex", "fp-hex@test.com", "FP Hex");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "FP Hex Agent");

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PutAsJsonAsync($"/api/agents/{agent.Id}/folder-preferences", new
        {
            Path = "/hex/path",
            IsFavorite = false,
            Color = "#AABBCC"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify database state
        using var scope3 = factory.Services.CreateScope();
        var db3 = scope3.ServiceProvider.GetRequiredService<NestDbContext>();
        var dbPref = await db3.UserFolderPreferences.FirstOrDefaultAsync(p => p.AgentId == agent.Id && p.Path == "/hex/path");
        Assert.NotNull(dbPref);
        Assert.False(dbPref.IsFavorite);
        Assert.Equal("#AABBCC", dbPref.Color);
    }

    [Fact]
    public async Task DeletePreference_RemovesPreference()
    {
        var user = new TestUser("auth0|fp-delete", "fp-delete@test.com", "FP Delete");
        var (seededUser, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        var agent = await TestDatabaseHelper.SeedAgentAsync(factory.Services, account.Id, "FP Delete Agent");
        var pref = await TestDatabaseHelper.SeedFolderPreferenceAsync(factory.Services, seededUser.Id, agent.Id, "/delete/path");

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.DeleteAsync($"/api/agents/{agent.Id}/folder-preferences/{pref.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify database state
        using var scope4 = factory.Services.CreateScope();
        var db4 = scope4.ServiceProvider.GetRequiredService<NestDbContext>();
        var dbPref = await db4.UserFolderPreferences.FirstOrDefaultAsync(p => p.Id == pref.Id);
        Assert.Null(dbPref);
    }
}
