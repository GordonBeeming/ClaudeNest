using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.IntegrationTests.Infrastructure;
using ClaudeNest.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeNest.Backend.IntegrationTests.Controllers;

public class AdminCouponsControllerTests(ClaudeNestWebApplicationFactory factory) : IClassFixture<ClaudeNestWebApplicationFactory>
{
    [Fact]
    public async Task ListCoupons_ReturnsCoupons()
    {
        var admin = new TestUser("auth0|ac-list", "ac-list@test.com", "AC List");
        var (adminSeeded, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);
        await TestDatabaseHelper.SeedCouponAsync(factory.Services, adminSeeded.Id,
            ClaudeNestWebApplicationFactory.HawkPlanId, code: "AC-LIST-COUPON");

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.GetAsync("/api/admin/coupons");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var coupons = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(coupons.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task ListCoupons_Returns403_ForNonAdmin()
    {
        var user = new TestUser("auth0|ac-nonadmin", "ac-nonadmin@test.com", "AC NonAdmin");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/admin/coupons");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateCoupon_CreatesCoupon()
    {
        var admin = new TestUser("auth0|ac-create", "ac-create@test.com", "AC Create");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PostAsJsonAsync("/api/admin/coupons", new
        {
            Code = "AC-NEW-COUPON",
            PlanId = ClaudeNestWebApplicationFactory.HawkPlanId,
            FreeMonths = 3,
            MaxRedemptions = 50,
            DiscountType = "FreeMonths"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("AC-NEW-COUPON", body.GetProperty("code").GetString());
        Assert.Equal(3, body.GetProperty("freeMonths").GetInt32());
    }

    [Fact]
    public async Task CreateCoupon_ReturnsBadRequest_ForEmptyCode()
    {
        var admin = new TestUser("auth0|ac-empty-code", "ac-empty-code@test.com", "AC EmptyCode");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PostAsJsonAsync("/api/admin/coupons", new
        {
            Code = "",
            PlanId = ClaudeNestWebApplicationFactory.HawkPlanId,
            FreeMonths = 1,
            MaxRedemptions = 10,
            DiscountType = "FreeMonths"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCoupon_ReturnsBadRequest_ForDuplicateCode()
    {
        var admin = new TestUser("auth0|ac-dupe", "ac-dupe@test.com", "AC Dupe");
        var (adminSeeded, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);
        await TestDatabaseHelper.SeedCouponAsync(factory.Services, adminSeeded.Id,
            ClaudeNestWebApplicationFactory.HawkPlanId, code: "AC-DUPE-CODE");

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PostAsJsonAsync("/api/admin/coupons", new
        {
            Code = "AC-DUPE-CODE",
            PlanId = ClaudeNestWebApplicationFactory.HawkPlanId,
            FreeMonths = 1,
            MaxRedemptions = 10,
            DiscountType = "FreeMonths"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCoupon_ReturnsBadRequest_ForInvalidDiscount()
    {
        var admin = new TestUser("auth0|ac-invalid-disc", "ac-invalid-disc@test.com", "AC InvalidDisc");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        var client = factory.CreateAuthenticatedClient(admin);

        // PercentOff discount type without PercentOff value
        var response = await client.PostAsJsonAsync("/api/admin/coupons", new
        {
            Code = "AC-INVALID-DISC",
            PlanId = ClaudeNestWebApplicationFactory.HawkPlanId,
            FreeMonths = 0,
            MaxRedemptions = 10,
            DiscountType = "PercentOff"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCoupon_UpdatesCoupon()
    {
        var admin = new TestUser("auth0|ac-update", "ac-update@test.com", "AC Update");
        var (adminSeeded, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);
        var coupon = await TestDatabaseHelper.SeedCouponAsync(factory.Services, adminSeeded.Id,
            ClaudeNestWebApplicationFactory.HawkPlanId, code: "AC-UPDATE");

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PutAsJsonAsync($"/api/admin/coupons/{coupon.Id}", new
        {
            MaxRedemptions = 999
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(999, body.GetProperty("maxRedemptions").GetInt32());
    }

    [Fact]
    public async Task DeactivateCoupon_DeactivatesCoupon()
    {
        var admin = new TestUser("auth0|ac-deactivate", "ac-deactivate@test.com", "AC Deactivate");
        var (adminSeeded, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);
        var coupon = await TestDatabaseHelper.SeedCouponAsync(factory.Services, adminSeeded.Id,
            ClaudeNestWebApplicationFactory.HawkPlanId, code: "AC-DEACTIVATE");

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.DeleteAsync($"/api/admin/coupons/{coupon.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task DeactivateCoupon_DeactivatesStripeCoupon()
    {
        var admin = new TestUser("auth0|ac-stripe-deact", "ac-stripe-deact@test.com", "AC StripeDeact");
        var (adminSeeded, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);
        var coupon = await TestDatabaseHelper.SeedCouponAsync(factory.Services, adminSeeded.Id,
            ClaudeNestWebApplicationFactory.HawkPlanId, code: "AC-STRIPE-DEACT");

        // Set Stripe coupon ID
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var c = await db.Coupons.AsTracking().FirstAsync(c => c.Id == coupon.Id);
            c.StripeCouponId = "stripe_coupon_test";
            await db.SaveChangesAsync();
        }

        var client = factory.CreateAuthenticatedClient(admin);

        await client.DeleteAsync($"/api/admin/coupons/{coupon.Id}");

        Assert.Contains(factory.FakeStripe.Calls, c => c.Contains("DeactivateStripeCoupon:stripe_coupon_test"));
    }
}
