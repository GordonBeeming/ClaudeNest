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
        // Seed an admin user to create the coupon
        var adminUser = new TestUser("auth0|plans-coupon-admin", "plans-coupon-admin@test.com", "Plans Coupon Admin");
        var (user, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, adminUser, isAdmin: true);

        var coupon = await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, user.Id, ClaudeNestWebApplicationFactory.WrenPlanId,
            code: "PLANS-DEFAULT", freeMonths: 2);

        // Set as default coupon on Wren plan
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClaudeNest.Backend.Data.NestDbContext>();
            var plan = await db.Plans.AsTracking().FirstAsync(p => p.Id == ClaudeNestWebApplicationFactory.WrenPlanId);
            plan.DefaultCouponId = coupon.Id;
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var plans = await response.Content.ReadFromJsonAsync<JsonElement>();
        var wrenPlan = plans.EnumerateArray().First(p => p.GetProperty("name").GetString() == "Wren");
        Assert.NotEqual(JsonValueKind.Null, wrenPlan.GetProperty("defaultCoupon").ValueKind);
        Assert.Equal(2, wrenPlan.GetProperty("defaultCoupon").GetProperty("freeMonths").GetInt32());

        // Clean up default coupon
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClaudeNest.Backend.Data.NestDbContext>();
            var plan = await db.Plans.AsTracking().FirstAsync(p => p.Id == ClaudeNestWebApplicationFactory.WrenPlanId);
            plan.DefaultCouponId = null;
            await db.SaveChangesAsync();
        }
    }
}
