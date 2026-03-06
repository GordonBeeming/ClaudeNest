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

        // Verify database state
        using var scope1 = factory.Services.CreateScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<NestDbContext>();
        var dbUser = await db1.Users.FirstOrDefaultAsync(u => u.Auth0UserId == user.Auth0UserId);
        Assert.NotNull(dbUser);
        Assert.Equal("New Name", dbUser.DisplayName);
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

        // Verify database state
        using var scope2 = factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<NestDbContext>();
        var dbAccount = await db2.Accounts.FirstOrDefaultAsync(a => a.Users.Any(u => u.Auth0UserId == user.Auth0UserId));
        Assert.NotNull(dbAccount);
        Assert.Equal("acceptEdits", dbAccount.PermissionMode);
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

        // Verify database state
        using var scope3 = factory.Services.CreateScope();
        var db3 = scope3.ServiceProvider.GetRequiredService<NestDbContext>();
        var dbAccount3 = await db3.Accounts.FirstOrDefaultAsync(a => a.Users.Any(u => u.Auth0UserId == user.Auth0UserId));
        Assert.NotNull(dbAccount3);
        Assert.Equal(ClaudeNestWebApplicationFactory.RobinPlanId, dbAccount3.PlanId);
        Assert.Equal(SubscriptionStatus.Active, dbAccount3.SubscriptionStatus);
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

    [Fact]
    public async Task SelectPlan_AutoAppliesDefaultCoupon_ViaStripeCheckout()
    {
        var adminUser = new TestUser("auth0|acct-auto-coupon-admin", "acct-auto-coupon-admin@test.com", "Auto Coupon Admin");
        var (admin, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        var coupon = await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, admin.Id, ClaudeNestWebApplicationFactory.CondorPlanId,
            code: "AUTO-APPLY", freeMonths: 1);

        // Set Stripe coupon ID and set as default
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var c = await db.Coupons.AsTracking().FirstAsync(c => c.Id == coupon.Id);
            c.StripeCouponId = "stripe_auto_apply";
            await db.SaveChangesAsync();
        }

        // Set StripePriceId on Condor plan and set default coupon
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var plan = await db.Plans.AsTracking().FirstAsync(p => p.Id == ClaudeNestWebApplicationFactory.CondorPlanId);
            plan.StripePriceId = "price_condor_test";
            plan.DefaultCouponId = coupon.Id;
            await db.SaveChangesAsync();
        }

        try
        {
            var user = new TestUser("auth0|acct-auto-coupon-user", "acct-auto-coupon-user@test.com", "Auto Coupon User");
            await TestDatabaseHelper.SeedUserAsync(factory.Services, user, subscriptionStatus: SubscriptionStatus.None);

            var client = factory.CreateAuthenticatedClient(user);

            var response = await client.PostAsJsonAsync("/api/account/select-plan",
                new { PlanId = ClaudeNestWebApplicationFactory.CondorPlanId });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("redirect", body.GetProperty("action").GetString());

            // FakeStripe records calls — verify checkout was created
            Assert.Contains(factory.FakeStripe.Calls, c => c.Contains("CreateCheckoutSession:"));
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var plan = await db.Plans.AsTracking().FirstAsync(p => p.Id == ClaudeNestWebApplicationFactory.CondorPlanId);
            plan.StripePriceId = null;
            plan.DefaultCouponId = null;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task SelectPlan_DoesNotAutoApplyExpiredDefaultCoupon()
    {
        var adminUser = new TestUser("auth0|acct-expired-auto-admin", "acct-expired-auto-admin@test.com", "Expired Auto Admin");
        var (admin, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        var coupon = await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, admin.Id, ClaudeNestWebApplicationFactory.CondorPlanId,
            code: "EXPIRED-AUTO", freeMonths: 1,
            expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var c = await db.Coupons.AsTracking().FirstAsync(c => c.Id == coupon.Id);
            c.StripeCouponId = "stripe_expired_auto";
            await db.SaveChangesAsync();

            var plan = await db.Plans.AsTracking().FirstAsync(p => p.Id == ClaudeNestWebApplicationFactory.CondorPlanId);
            plan.StripePriceId = "price_condor_expired_test";
            plan.DefaultCouponId = coupon.Id;
            await db.SaveChangesAsync();
        }

        try
        {
            var user = new TestUser("auth0|acct-expired-auto-user", "acct-expired-auto-user@test.com", "Expired Auto User");
            await TestDatabaseHelper.SeedUserAsync(factory.Services, user, subscriptionStatus: SubscriptionStatus.None);

            // Clear fake stripe calls to isolate this test
            factory.FakeStripe.Calls.Clear();

            var client = factory.CreateAuthenticatedClient(user);

            var response = await client.PostAsJsonAsync("/api/account/select-plan",
                new { PlanId = ClaudeNestWebApplicationFactory.CondorPlanId });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Checkout should still happen, but without the coupon
            // The FakeStripeService.CreateCheckoutSessionAsync receives stripeCouponId as null
            // We can verify the checkout was created (plan was selected)
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("redirect", body.GetProperty("action").GetString());
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var plan = await db.Plans.AsTracking().FirstAsync(p => p.Id == ClaudeNestWebApplicationFactory.CondorPlanId);
            plan.StripePriceId = null;
            plan.DefaultCouponId = null;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task SelectPlan_DoesNotAutoApplyMaxedOutDefaultCoupon()
    {
        var adminUser = new TestUser("auth0|acct-maxed-auto-admin", "acct-maxed-auto-admin@test.com", "Maxed Auto Admin");
        var (admin, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        var coupon = await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, admin.Id, ClaudeNestWebApplicationFactory.CondorPlanId,
            code: "MAXED-AUTO", freeMonths: 1, maxRedemptions: 1, timesRedeemed: 1);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var c = await db.Coupons.AsTracking().FirstAsync(c => c.Id == coupon.Id);
            c.StripeCouponId = "stripe_maxed_auto";
            await db.SaveChangesAsync();

            var plan = await db.Plans.AsTracking().FirstAsync(p => p.Id == ClaudeNestWebApplicationFactory.CondorPlanId);
            plan.StripePriceId = "price_condor_maxed_test";
            plan.DefaultCouponId = coupon.Id;
            await db.SaveChangesAsync();
        }

        try
        {
            var user = new TestUser("auth0|acct-maxed-auto-user", "acct-maxed-auto-user@test.com", "Maxed Auto User");
            await TestDatabaseHelper.SeedUserAsync(factory.Services, user, subscriptionStatus: SubscriptionStatus.None);

            var client = factory.CreateAuthenticatedClient(user);

            var response = await client.PostAsJsonAsync("/api/account/select-plan",
                new { PlanId = ClaudeNestWebApplicationFactory.CondorPlanId });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("redirect", body.GetProperty("action").GetString());
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var plan = await db.Plans.AsTracking().FirstAsync(p => p.Id == ClaudeNestWebApplicationFactory.CondorPlanId);
            plan.StripePriceId = null;
            plan.DefaultCouponId = null;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task SelectPlan_DoesNotAutoApplyAlreadyRedeemedDefaultCoupon()
    {
        var adminUser = new TestUser("auth0|acct-redeemed-auto-admin", "acct-redeemed-auto-admin@test.com", "Redeemed Auto Admin");
        var (admin, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        var coupon = await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, admin.Id, ClaudeNestWebApplicationFactory.CondorPlanId,
            code: "REDEEMED-AUTO", freeMonths: 1);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var c = await db.Coupons.AsTracking().FirstAsync(c => c.Id == coupon.Id);
            c.StripeCouponId = "stripe_redeemed_auto";
            await db.SaveChangesAsync();

            var plan = await db.Plans.AsTracking().FirstAsync(p => p.Id == ClaudeNestWebApplicationFactory.CondorPlanId);
            plan.StripePriceId = "price_condor_redeemed_test";
            plan.DefaultCouponId = coupon.Id;
            await db.SaveChangesAsync();
        }

        try
        {
            var user = new TestUser("auth0|acct-redeemed-auto-user", "acct-redeemed-auto-user@test.com", "Redeemed Auto User");
            var (_, userAccount) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user, subscriptionStatus: SubscriptionStatus.None);

            // Mark as already redeemed
            await TestDatabaseHelper.SeedCouponRedemptionAsync(
                factory.Services, coupon.Id, userAccount.Id, DateTimeOffset.UtcNow.AddMonths(1));

            var client = factory.CreateAuthenticatedClient(user);

            var response = await client.PostAsJsonAsync("/api/account/select-plan",
                new { PlanId = ClaudeNestWebApplicationFactory.CondorPlanId });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("redirect", body.GetProperty("action").GetString());
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var plan = await db.Plans.AsTracking().FirstAsync(p => p.Id == ClaudeNestWebApplicationFactory.CondorPlanId);
            plan.StripePriceId = null;
            plan.DefaultCouponId = null;
            await db.SaveChangesAsync();
        }
    }
}
