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

    [Fact]
    public async Task GetCouponByCode_ReturnsValidCouponWithPlanDetails()
    {
        var adminUser = new TestUser("auth0|plans-coupon-lookup-admin", "plans-coupon-lookup-admin@test.com", "Plans Coupon Lookup Admin");
        var (user, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, user.Id, ClaudeNestWebApplicationFactory.RobinPlanId,
            code: "LINKTEST1", freeMonths: 3);

        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/plans/coupon/LINKTEST1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("valid").GetBoolean());
        Assert.Equal("LINKTEST1", result.GetProperty("code").GetString());
        Assert.Equal("Robin", result.GetProperty("planName").GetString());
        Assert.Equal(3, result.GetProperty("freeMonths").GetInt32());
        Assert.Equal("FreeMonths", result.GetProperty("discountType").GetString());
        Assert.True(result.GetProperty("planPriceCents").GetInt32() > 0);
        Assert.True(result.GetProperty("planMaxAgents").GetInt32() > 0);
        Assert.True(result.GetProperty("planMaxSessions").GetInt32() > 0);
    }

    [Fact]
    public async Task GetCouponByCode_CaseInsensitive()
    {
        var adminUser = new TestUser("auth0|plans-coupon-case-admin", "plans-coupon-case-admin@test.com", "Plans Coupon Case Admin");
        var (user, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, user.Id, ClaudeNestWebApplicationFactory.WrenPlanId,
            code: "CASETEST1", freeMonths: 1);

        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/plans/coupon/casetest1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("valid").GetBoolean());
        Assert.Equal("CASETEST1", result.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetCouponByCode_ReturnsInvalid_WhenNotFound()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/plans/coupon/DOESNOTEXIST");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(result.GetProperty("valid").GetBoolean());
        Assert.Equal("Coupon not found", result.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task GetCouponByCode_ReturnsInvalid_WhenExpired()
    {
        var adminUser = new TestUser("auth0|plans-coupon-exp-admin", "plans-coupon-exp-admin@test.com", "Plans Coupon Exp Admin");
        var (user, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, user.Id, ClaudeNestWebApplicationFactory.WrenPlanId,
            code: "EXPIREDLINK1", freeMonths: 1,
            expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/plans/coupon/EXPIREDLINK1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(result.GetProperty("valid").GetBoolean());
        Assert.Equal("Coupon has expired", result.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task GetCouponByCode_ReturnsInvalid_WhenInactive()
    {
        var adminUser = new TestUser("auth0|plans-coupon-inact-admin", "plans-coupon-inact-admin@test.com", "Plans Coupon Inact Admin");
        var (user, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, user.Id, ClaudeNestWebApplicationFactory.WrenPlanId,
            code: "INACTIVELINK1", freeMonths: 1, isActive: false);

        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/plans/coupon/INACTIVELINK1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(result.GetProperty("valid").GetBoolean());
        Assert.Equal("Coupon is no longer active", result.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task GetCouponByCode_ReturnsInvalid_WhenMaxedOut()
    {
        var adminUser = new TestUser("auth0|plans-coupon-max-admin", "plans-coupon-max-admin@test.com", "Plans Coupon Max Admin");
        var (user, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, user.Id, ClaudeNestWebApplicationFactory.WrenPlanId,
            code: "MAXEDLINK1", freeMonths: 1, maxRedemptions: 2, timesRedeemed: 2);

        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/plans/coupon/MAXEDLINK1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(result.GetProperty("valid").GetBoolean());
        Assert.Equal("Coupon has reached maximum redemptions", result.GetProperty("reason").GetString());
    }
}
