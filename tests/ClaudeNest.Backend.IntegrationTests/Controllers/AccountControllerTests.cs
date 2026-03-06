using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.IntegrationTests.Infrastructure;
using ClaudeNest.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeNest.Backend.IntegrationTests.Controllers;

public class AccountControllerTests(ClaudeNestWebApplicationFactory factory) : IClassFixture<ClaudeNestWebApplicationFactory>
{
    [Fact]
    public async Task GetAccount_ReturnsAccountDetails()
    {
        var user = new TestUser("auth0|acct-get", "acct-get@test.com", "Acct Get");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/account");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Hawk", body.GetProperty("planName").GetString());
    }

    [Fact]
    public async Task GetAccount_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = factory.CreateAuthenticatedClient(TestUsers.Anonymous);

        var response = await client.GetAsync("/api/account");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDisplayName_UpdatesName()
    {
        var user = new TestUser("auth0|acct-name", "acct-name@test.com", "Acct Name");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PutAsJsonAsync("/api/account/display-name", new { DisplayName = "New Name" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("New Name", body.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task UpdateDisplayName_ReturnsBadRequest_ForEmptyName()
    {
        var user = new TestUser("auth0|acct-empty-name", "acct-empty-name@test.com", "Acct EmptyName");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PutAsJsonAsync("/api/account/display-name", new { DisplayName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDisplayName_ReturnsBadRequest_ForTooLongName()
    {
        var user = new TestUser("auth0|acct-long-name", "acct-long-name@test.com", "Acct LongName");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PutAsJsonAsync("/api/account/display-name", new { DisplayName = new string('a', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetPermissionMode_SetsMode()
    {
        var user = new TestUser("auth0|acct-perm", "acct-perm@test.com", "Acct Perm");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PostAsJsonAsync("/api/account/permission-mode", new { Mode = "acceptEdits" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("acceptEdits", body.GetProperty("permissionMode").GetString());
    }

    [Fact]
    public async Task SetPermissionMode_ReturnsBadRequest_ForInvalidMode()
    {
        var user = new TestUser("auth0|acct-bad-perm", "acct-bad-perm@test.com", "Acct BadPerm");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PostAsJsonAsync("/api/account/permission-mode", new { Mode = "invalidMode" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SelectPlan_LocalAssignment_WhenNoStripePrice()
    {
        var user = new TestUser("auth0|acct-select-plan", "acct-select-plan@test.com", "Acct SelectPlan");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user, subscriptionStatus: SubscriptionStatus.None);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PostAsJsonAsync("/api/account/select-plan",
            new { PlanId = ClaudeNestWebApplicationFactory.RobinPlanId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("local", body.GetProperty("action").GetString());
    }

    [Fact]
    public async Task SelectPlan_ReturnsBadRequest_ForInvalidPlan()
    {
        var user = new TestUser("auth0|acct-bad-plan", "acct-bad-plan@test.com", "Acct BadPlan");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PostAsJsonAsync("/api/account/select-plan",
            new { PlanId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SelectPlan_BlocksChangeWithActiveSubscription()
    {
        var user = new TestUser("auth0|acct-block-plan", "acct-block-plan@test.com", "Acct BlockPlan");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        // Set a Stripe subscription on the account
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var acct = await db.Accounts.AsTracking().FirstAsync(a => a.Id == account.Id);
            acct.StripeSubscriptionId = "sub_test_123";
            acct.SubscriptionStatus = SubscriptionStatus.Active;
            await db.SaveChangesAsync();
        }

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PostAsJsonAsync("/api/account/select-plan",
            new { PlanId = ClaudeNestWebApplicationFactory.EaglePlanId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BillingPortal_ReturnsBadRequest_WhenNoCustomer()
    {
        var user = new TestUser("auth0|acct-no-cust", "acct-no-cust@test.com", "Acct NoCust");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PostAsync("/api/account/billing-portal", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RedeemCoupon_ReturnsValid_ForValidCoupon()
    {
        var adminUser = new TestUser("auth0|acct-coupon-admin", "acct-coupon-admin@test.com", "Coupon Admin");
        var (admin, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        var coupon = await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, admin.Id, ClaudeNestWebApplicationFactory.HawkPlanId,
            code: "ACCT-VALID-COUPON");

        var user = new TestUser("auth0|acct-coupon-user", "acct-coupon-user@test.com", "Coupon User");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PostAsJsonAsync("/api/account/redeem-coupon", new { Code = "ACCT-VALID-COUPON" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task RedeemCoupon_ReturnsInvalid_ForInactiveCoupon()
    {
        var adminUser = new TestUser("auth0|acct-inactive-coupon-admin", "acct-inactive-coupon-admin@test.com", "Inactive Coupon Admin");
        var (admin, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, admin.Id, ClaudeNestWebApplicationFactory.HawkPlanId,
            code: "ACCT-INACTIVE", isActive: false);

        var user = new TestUser("auth0|acct-inactive-user", "acct-inactive-user@test.com", "Inactive User");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PostAsJsonAsync("/api/account/redeem-coupon", new { Code = "ACCT-INACTIVE" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task RedeemCoupon_ReturnsInvalid_ForAlreadyRedeemed()
    {
        var adminUser = new TestUser("auth0|acct-already-admin", "acct-already-admin@test.com", "Already Admin");
        var (admin, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        var coupon = await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, admin.Id, ClaudeNestWebApplicationFactory.HawkPlanId,
            code: "ACCT-ALREADY");

        var user = new TestUser("auth0|acct-already-user", "acct-already-user@test.com", "Already User");
        var (_, userAccount) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        // Already redeemed
        await TestDatabaseHelper.SeedCouponRedemptionAsync(
            factory.Services, coupon.Id, userAccount.Id, DateTimeOffset.UtcNow.AddMonths(1));

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.PostAsJsonAsync("/api/account/redeem-coupon", new { Code = "ACCT-ALREADY" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("valid").GetBoolean());
    }
}
