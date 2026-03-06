using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeNest.Backend.IntegrationTests.Infrastructure;
using ClaudeNest.Shared.Enums;

namespace ClaudeNest.Backend.IntegrationTests.Controllers;

public class AccountLedgerControllerTests(ClaudeNestWebApplicationFactory factory) : IClassFixture<ClaudeNestWebApplicationFactory>
{
    [Fact]
    public async Task GetLedger_ReturnsEntries()
    {
        var user = new TestUser("auth0|ledger-get", "ledger-get@test.com", "Ledger Get");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);
        await TestDatabaseHelper.SeedLedgerEntryAsync(factory.Services, account.Id, description: "Ledger test entry");

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/account/ledger");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 1);
        Assert.True(body.GetProperty("items").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task GetLedger_PaginatesCorrectly()
    {
        var user = new TestUser("auth0|ledger-page", "ledger-page@test.com", "Ledger Page");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        // Seed multiple entries
        for (int i = 0; i < 5; i++)
        {
            await TestDatabaseHelper.SeedLedgerEntryAsync(factory.Services, account.Id, description: $"Page entry {i}");
        }

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/account/ledger?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 5);
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(2, body.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task GetLedger_ClampsPageSize()
    {
        var user = new TestUser("auth0|ledger-clamp", "ledger-clamp@test.com", "Ledger Clamp");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/account/ledger?pageSize=500");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(100, body.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task GetLedger_ReturnsOnlyOwnEntries()
    {
        var ownerUser = new TestUser("auth0|ledger-own", "ledger-own@test.com", "Ledger Own");
        var (_, ownerAccount) = await TestDatabaseHelper.SeedUserAsync(factory.Services, ownerUser);
        await TestDatabaseHelper.SeedLedgerEntryAsync(factory.Services, ownerAccount.Id, description: "Owner entry");

        var otherUser = new TestUser("auth0|ledger-other", "ledger-other@test.com", "Ledger Other");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, otherUser);

        var client = factory.CreateAuthenticatedClient(otherUser);

        var response = await client.GetAsync("/api/account/ledger");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Other user should not see owner's entries
        var descriptions = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("description").GetString()).ToList();
        Assert.DoesNotContain("Owner entry", descriptions);
    }

    [Fact]
    public async Task GetLedger_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = factory.CreateAuthenticatedClient(TestUsers.Anonymous);

        var response = await client.GetAsync("/api/account/ledger");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
