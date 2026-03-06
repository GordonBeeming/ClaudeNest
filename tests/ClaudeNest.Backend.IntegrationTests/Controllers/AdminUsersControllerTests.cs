using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.IntegrationTests.Infrastructure;
using ClaudeNest.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeNest.Backend.IntegrationTests.Controllers;

public class AdminUsersControllerTests(ClaudeNestWebApplicationFactory factory) : IClassFixture<ClaudeNestWebApplicationFactory>
{
    [Fact]
    public async Task ListUsers_ReturnsAllUsers()
    {
        var admin = new TestUser("auth0|au-list", "au-list@test.com", "AU List");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task ListUsers_FiltersByDomain()
    {
        var admin = new TestUser("auth0|au-domain", "au-domain@test.com", "AU Domain");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        // Seed a user with a specific domain
        var domainUser = new TestUser("auth0|au-domain-target", "target@filterdomain.com", "Domain Target");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, domainUser);

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.GetAsync("/api/admin/users?domain=filterdomain.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        foreach (var item in items.EnumerateArray())
        {
            Assert.Contains("filterdomain.com", item.GetProperty("email").GetString());
        }
    }

    [Fact]
    public async Task ListUsers_Paginates()
    {
        var admin = new TestUser("auth0|au-paginate", "au-paginate@test.com", "AU Paginate");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.GetAsync("/api/admin/users?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("items").GetArrayLength() <= 2);
    }

    [Fact]
    public async Task ListUsers_Returns403_ForNonAdmin()
    {
        var user = new TestUser("auth0|au-nonadmin", "au-nonadmin@test.com", "AU NonAdmin");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        var client = factory.CreateAuthenticatedClient(user);

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CancelSubscription_CancelsStripeSubscription()
    {
        var admin = new TestUser("auth0|au-cancel", "au-cancel@test.com", "AU Cancel");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        var targetUser = new TestUser("auth0|au-cancel-target", "au-cancel-target@test.com", "AU CancelTarget");
        var (target, targetAccount) = await TestDatabaseHelper.SeedUserAsync(factory.Services, targetUser);

        // Give the target a Stripe subscription
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var acct = await db.Accounts.AsTracking().FirstAsync(a => a.Id == targetAccount.Id);
            acct.StripeSubscriptionId = "sub_cancel_test";
            acct.StripeCustomerId = "cus_cancel_test";
            await db.SaveChangesAsync();
        }

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PostAsync($"/api/admin/users/{target.Id}/cancel-subscription", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify subscription was cancelled in DB
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var acct = await db.Accounts.FirstAsync(a => a.Id == targetAccount.Id);
            Assert.Equal(SubscriptionStatus.Cancelled, acct.SubscriptionStatus);
            Assert.Null(acct.StripeSubscriptionId);
        }
    }

    [Fact]
    public async Task CancelSubscription_ReturnsBadRequest_WhenNoSubscription()
    {
        var admin = new TestUser("auth0|au-cancel-nosub", "au-cancel-nosub@test.com", "AU CancelNoSub");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        var targetUser = new TestUser("auth0|au-cancel-nosub-target", "au-cancel-nosub-target@test.com", "NoSub Target");
        var (target, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, targetUser);

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PostAsync($"/api/admin/users/{target.Id}/cancel-subscription", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ToggleAdmin_TogglesAdminFlag()
    {
        var admin = new TestUser("auth0|au-toggle", "au-toggle@test.com", "AU Toggle");
        await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        var targetUser = new TestUser("auth0|au-toggle-target", "au-toggle-target@test.com", "Toggle Target");
        var (target, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, targetUser);

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PostAsync($"/api/admin/users/{target.Id}/toggle-admin", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task ToggleAdmin_BlocksSelfToggle()
    {
        var admin = new TestUser("auth0|au-self-toggle", "au-self-toggle@test.com", "AU SelfToggle");
        var (seededAdmin, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PostAsync($"/api/admin/users/{seededAdmin.Id}/toggle-admin", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GiveCoupon_GivesCouponToUser()
    {
        var admin = new TestUser("auth0|au-give", "au-give@test.com", "AU Give");
        var (adminSeeded, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        var coupon = await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, adminSeeded.Id, ClaudeNestWebApplicationFactory.HawkPlanId,
            code: "AU-GIVE-COUPON");

        var targetUser = new TestUser("auth0|au-give-target", "au-give-target@test.com", "Give Target");
        var (target, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, targetUser, subscriptionStatus: SubscriptionStatus.None);

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PostAsJsonAsync($"/api/admin/users/{target.Id}/give-coupon",
            new { CouponId = coupon.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GiveCoupon_ReturnsBadRequest_ForInactiveCoupon()
    {
        var admin = new TestUser("auth0|au-give-inactive", "au-give-inactive@test.com", "AU GiveInactive");
        var (adminSeeded, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        var coupon = await TestDatabaseHelper.SeedCouponAsync(
            factory.Services, adminSeeded.Id, ClaudeNestWebApplicationFactory.HawkPlanId,
            code: "AU-INACTIVE-COUPON", isActive: false);

        var targetUser = new TestUser("auth0|au-give-inactive-target", "au-give-inactive-target@test.com", "GiveInactive Target");
        var (target, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, targetUser);

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PostAsJsonAsync($"/api/admin/users/{target.Id}/give-coupon",
            new { CouponId = coupon.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OverridePlan_OverridesPlan()
    {
        var admin = new TestUser("auth0|au-override", "au-override@test.com", "AU Override");
        var (adminSeeded, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        // Create a company deal for the domain
        await TestDatabaseHelper.SeedCompanyDealAsync(
            factory.Services, adminSeeded.Id, ClaudeNestWebApplicationFactory.HawkPlanId,
            "overridedomain.com");

        var targetUser = new TestUser("auth0|au-override-target", "target@overridedomain.com", "Override Target");
        var (target, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, targetUser,
            planId: ClaudeNestWebApplicationFactory.HawkPlanId);

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PostAsJsonAsync($"/api/admin/users/{target.Id}/override-plan",
            new { PlanId = ClaudeNestWebApplicationFactory.EaglePlanId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Eagle", body.GetProperty("planName").GetString());
    }

    [Fact]
    public async Task RevertPlan_RevertsToDealPlan()
    {
        var admin = new TestUser("auth0|au-revert", "au-revert@test.com", "AU Revert");
        var (adminSeeded, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, admin, isAdmin: true);

        await TestDatabaseHelper.SeedCompanyDealAsync(
            factory.Services, adminSeeded.Id, ClaudeNestWebApplicationFactory.HawkPlanId,
            "revertdomain.com");

        // Target user with an overridden plan
        var targetUser = new TestUser("auth0|au-revert-target", "target@revertdomain.com", "Revert Target");
        var (target, _) = await TestDatabaseHelper.SeedUserAsync(factory.Services, targetUser,
            planId: ClaudeNestWebApplicationFactory.EaglePlanId);

        var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PostAsync($"/api/admin/users/{target.Id}/revert-plan", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Hawk", body.GetProperty("planName").GetString());
    }
}
