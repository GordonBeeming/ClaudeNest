using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.IntegrationTests.Infrastructure;
using ClaudeNest.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeNest.Backend.IntegrationTests.Controllers;

public class AdminCompanyDealsControllerTests(ClaudeNestWebApplicationFactory factory) : IClassFixture<ClaudeNestWebApplicationFactory>
{
    [Fact]
    public async Task ListDeals_ReturnsDeals()
    {
        var admin = new TestUser("auth0|acd-list", "acd-list@test.com", "ACD List");
        var (adminSeeded, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);
        await TestDatabaseHelper.SeedCompanyDealAsync(factory.Services, adminSeeded.Id,
            ClaudeNestWebApplicationFactory.HawkPlanId, "acd-list.com");

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.GetAsync("/api/admin/company-deals");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var deals = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(deals.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task ListDeals_Returns403_ForNonAdmin()
    {
        var user = new TestUser("auth0|acd-nonadmin", "acd-nonadmin@test.com", "ACD NonAdmin");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/admin/company-deals");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateDeal_CreatesDeal()
    {
        var admin = new TestUser("auth0|acd-create", "acd-create@test.com", "ACD Create");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PostAsJsonAsync("/api/admin/company-deals", new
        {
            Domain = "acd-newdeal.com",
            PlanId = ClaudeNestWebApplicationFactory.EaglePlanId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("acd-newdeal.com", body.GetProperty("domain").GetString());
        Assert.Equal("Eagle", body.GetProperty("planName").GetString());

        // Verify database state
        var dealId = body.GetProperty("id").GetGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
        var dbDeal = await db.CompanyDeals.FirstOrDefaultAsync(d => d.Id == dealId);
        Assert.NotNull(dbDeal);
        Assert.Equal("acd-newdeal.com", dbDeal.Domain);
        Assert.Equal(ClaudeNestWebApplicationFactory.EaglePlanId, dbDeal.PlanId);
        Assert.True(dbDeal.IsActive);
    }

    [Fact]
    public async Task CreateDeal_ReturnsBadRequest_ForInvalidDomain()
    {
        var admin = new TestUser("auth0|acd-bad-domain", "acd-bad-domain@test.com", "ACD BadDomain");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PostAsJsonAsync("/api/admin/company-deals", new
        {
            Domain = "nodot",
            PlanId = ClaudeNestWebApplicationFactory.HawkPlanId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateDeal_ReturnsBadRequest_ForDuplicateDomain()
    {
        var admin = new TestUser("auth0|acd-dupe", "acd-dupe@test.com", "ACD Dupe");
        var (adminSeeded, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);
        await TestDatabaseHelper.SeedCompanyDealAsync(factory.Services, adminSeeded.Id,
            ClaudeNestWebApplicationFactory.HawkPlanId, "acd-dupe.com");

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PostAsJsonAsync("/api/admin/company-deals", new
        {
            Domain = "acd-dupe.com",
            PlanId = ClaudeNestWebApplicationFactory.HawkPlanId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateDeal_ReturnsBadRequest_ForInvalidPlan()
    {
        var admin = new TestUser("auth0|acd-bad-plan", "acd-bad-plan@test.com", "ACD BadPlan");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PostAsJsonAsync("/api/admin/company-deals", new
        {
            Domain = "acd-badplan.com",
            PlanId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDeal_UpdatesDealPlan()
    {
        var admin = new TestUser("auth0|acd-update", "acd-update@test.com", "ACD Update");
        var (adminSeeded, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);
        var deal = await TestDatabaseHelper.SeedCompanyDealAsync(factory.Services, adminSeeded.Id,
            ClaudeNestWebApplicationFactory.HawkPlanId, "acd-update.com");

        var client = factory.CreateAuthenticatedClient(admin);

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/admin/company-deals/{deal.Id}")
        {
            Content = JsonContent.Create(new { PlanId = ClaudeNestWebApplicationFactory.EaglePlanId })
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Eagle", body.GetProperty("planName").GetString());

        // Verify database state
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
        var dbDeal = await db.CompanyDeals.FirstOrDefaultAsync(d => d.Id == deal.Id);
        Assert.NotNull(dbDeal);
        Assert.Equal(ClaudeNestWebApplicationFactory.EaglePlanId, dbDeal.PlanId);
    }

    [Fact]
    public async Task UpdateDeal_CascadesToAccounts()
    {
        var admin = new TestUser("auth0|acd-cascade", "acd-cascade@test.com", "ACD Cascade");
        var (adminSeeded, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);
        var deal = await TestDatabaseHelper.SeedCompanyDealAsync(factory.Services, adminSeeded.Id,
            ClaudeNestWebApplicationFactory.RobinPlanId, "acd-cascade.com");

        // Seed a user on this domain with the old plan
        var domainUser = new TestUser("auth0|acd-cascade-user", "user@acd-cascade.com", "Cascade User");
        var (_, userAccount) = await TestDatabaseHelper.SeedUserAsync(factory.Services, domainUser,
            planId: ClaudeNestWebApplicationFactory.RobinPlanId);

        var client = factory.CreateAuthenticatedClient(admin);

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/admin/company-deals/{deal.Id}")
        {
            Content = JsonContent.Create(new { PlanId = ClaudeNestWebApplicationFactory.EaglePlanId })
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify the user's account was updated
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
        var acct = await db.Accounts.FirstAsync(a => a.Id == userAccount.Id);
        Assert.Equal(ClaudeNestWebApplicationFactory.EaglePlanId, acct.PlanId);
    }

    [Fact]
    public async Task DeactivateDeal_DeactivatesDeal()
    {
        var admin = new TestUser("auth0|acd-deact", "acd-deact@test.com", "ACD Deact");
        var (adminSeeded, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);
        var deal = await TestDatabaseHelper.SeedCompanyDealAsync(factory.Services, adminSeeded.Id,
            ClaudeNestWebApplicationFactory.HawkPlanId, "acd-deact.com");

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PostAsync($"/api/admin/company-deals/{deal.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("isActive").GetBoolean());

        // Verify database state
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
        var dbDeal = await db.CompanyDeals.FirstOrDefaultAsync(d => d.Id == deal.Id);
        Assert.NotNull(dbDeal);
        Assert.False(dbDeal.IsActive);
        Assert.NotNull(dbDeal.DeactivatedAt);
    }

    [Fact]
    public async Task DeactivateDeal_CancelsAffectedAccounts()
    {
        var admin = new TestUser("auth0|acd-cancel", "acd-cancel@test.com", "ACD Cancel");
        var (adminSeeded, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);
        var deal = await TestDatabaseHelper.SeedCompanyDealAsync(factory.Services, adminSeeded.Id,
            ClaudeNestWebApplicationFactory.HawkPlanId, "acd-cancel.com");

        // Seed a user on this domain
        var domainUser = new TestUser("auth0|acd-cancel-user", "user@acd-cancel.com", "Cancel User");
        var (_, userAccount) = await TestDatabaseHelper.SeedUserAsync(factory.Services, domainUser,
            planId: ClaudeNestWebApplicationFactory.HawkPlanId);

        var client = factory.CreateAuthenticatedClient(admin);

        await client.PostAsync($"/api/admin/company-deals/{deal.Id}/deactivate", null);

        // Verify account was cancelled
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
        var acct = await db.Accounts.FirstAsync(a => a.Id == userAccount.Id);
        Assert.Equal(SubscriptionStatus.Cancelled, acct.SubscriptionStatus);
    }
}
