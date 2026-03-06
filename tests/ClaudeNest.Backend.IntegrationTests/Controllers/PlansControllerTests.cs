using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeNest.Backend.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeNest.Backend.IntegrationTests.Controllers;

public class PlansControllerTests(ClaudeNestWebApplicationFactory factory) : IClassFixture<ClaudeNestWebApplicationFactory>
{
    [Fact]
    public async Task GetPlans_ReturnsAllActivePlans()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var plans = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(6, plans.GetArrayLength());

        // Verify sorted by SortOrder
        var names = plans.EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToList();
        Assert.Equal(["Wren", "Robin", "Hawk", "Eagle", "Falcon", "Condor"], names);
    }

    [Fact]
    public async Task GetPlans_IncludesDefaultCouponInfo()
    {
        var adminUser = new TestUser("auth0|plans-coupon-admin", "plans-coupon-admin@test.com", "Plans Coupon Admin");
        var (user, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        var coupon = await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, user.Id, ClaudeNestWebApplicationFactory.WrenPlanId,
            code: "PLANS-DEFAULT", freeMonths: 2);

        await TestDatabaseHelper.SetPlanDefaultCouponAsync(factory.Services,
            ClaudeNestWebApplicationFactory.WrenPlanId, coupon.Id);

        try
        {
            var client = factory.CreateClient();
            var response = await client.GetAsync("/api/plans");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var plans = await response.Content.ReadFromJsonAsync<JsonElement>();
            var wrenPlan = plans.EnumerateArray().First(p => p.GetProperty("name").GetString() == "Wren");
            var dc = wrenPlan.GetProperty("defaultCoupon");
            Assert.NotEqual(JsonValueKind.Null, dc.ValueKind);
            Assert.Equal(2, dc.GetProperty("freeMonths").GetInt32());
            Assert.Equal("FreeMonths", dc.GetProperty("discountType").GetString());
        }
        finally
        {
            await TestDatabaseHelper.SetPlanDefaultCouponAsync(factory.Services,
                ClaudeNestWebApplicationFactory.WrenPlanId, null);
        }
    }

    [Fact]
    public async Task GetPlans_HidesDefaultCoupon_WhenExpired()
    {
        var adminUser = new TestUser("auth0|plans-expired-admin", "plans-expired-admin@test.com", "Plans Expired Admin");
        var (user, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        var coupon = await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, user.Id, ClaudeNestWebApplicationFactory.WrenPlanId,
            code: "PLANS-EXPIRED", freeMonths: 1,
            expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        await TestDatabaseHelper.SetPlanDefaultCouponAsync(factory.Services,
            ClaudeNestWebApplicationFactory.WrenPlanId, coupon.Id);

        try
        {
            var client = factory.CreateClient();
            var response = await client.GetAsync("/api/plans");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var plans = await response.Content.ReadFromJsonAsync<JsonElement>();
            var wrenPlan = plans.EnumerateArray().First(p => p.GetProperty("name").GetString() == "Wren");
            Assert.Equal(JsonValueKind.Null, wrenPlan.GetProperty("defaultCoupon").ValueKind);
        }
        finally
        {
            await TestDatabaseHelper.SetPlanDefaultCouponAsync(factory.Services,
                ClaudeNestWebApplicationFactory.WrenPlanId, null);
        }
    }

    [Fact]
    public async Task GetPlans_HidesDefaultCoupon_WhenMaxedOut()
    {
        var adminUser = new TestUser("auth0|plans-maxed-admin", "plans-maxed-admin@test.com", "Plans Maxed Admin");
        var (user, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        var coupon = await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, user.Id, ClaudeNestWebApplicationFactory.WrenPlanId,
            code: "PLANS-MAXED", freeMonths: 1, maxRedemptions: 5, timesRedeemed: 5);

        await TestDatabaseHelper.SetPlanDefaultCouponAsync(factory.Services,
            ClaudeNestWebApplicationFactory.WrenPlanId, coupon.Id);

        try
        {
            var client = factory.CreateClient();
            var response = await client.GetAsync("/api/plans");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var plans = await response.Content.ReadFromJsonAsync<JsonElement>();
            var wrenPlan = plans.EnumerateArray().First(p => p.GetProperty("name").GetString() == "Wren");
            Assert.Equal(JsonValueKind.Null, wrenPlan.GetProperty("defaultCoupon").ValueKind);
        }
        finally
        {
            await TestDatabaseHelper.SetPlanDefaultCouponAsync(factory.Services,
                ClaudeNestWebApplicationFactory.WrenPlanId, null);
        }
    }

    [Fact]
    public async Task GetPlans_HidesDefaultCoupon_WhenInactive()
    {
        var adminUser = new TestUser("auth0|plans-inactive-admin", "plans-inactive-admin@test.com", "Plans Inactive Admin");
        var (user, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        var coupon = await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, user.Id, ClaudeNestWebApplicationFactory.WrenPlanId,
            code: "PLANS-INACTIVE", freeMonths: 1, isActive: false);

        await TestDatabaseHelper.SetPlanDefaultCouponAsync(factory.Services,
            ClaudeNestWebApplicationFactory.WrenPlanId, coupon.Id);

        try
        {
            var client = factory.CreateClient();
            var response = await client.GetAsync("/api/plans");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var plans = await response.Content.ReadFromJsonAsync<JsonElement>();
            var wrenPlan = plans.EnumerateArray().First(p => p.GetProperty("name").GetString() == "Wren");
            Assert.Equal(JsonValueKind.Null, wrenPlan.GetProperty("defaultCoupon").ValueKind);
        }
        finally
        {
            await TestDatabaseHelper.SetPlanDefaultCouponAsync(factory.Services,
                ClaudeNestWebApplicationFactory.WrenPlanId, null);
        }
    }
}
