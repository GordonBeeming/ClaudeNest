using System.Security.Cryptography;
using System.Text;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Data.Entities;
using ClaudeNest.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeNest.Backend.IntegrationTests.Infrastructure;

public static class TestDatabaseHelper
{
    public static async Task<(User user, Account account)> SeedUserAsync(
        IServiceProvider services,
        TestUser testUser,
        Guid? planId = null,
        bool isAdmin = false,
        SubscriptionStatus subscriptionStatus = SubscriptionStatus.Active)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();

        var account = new Account
        {
            Name = testUser.Name ?? "Test Account",
            PlanId = planId ?? ClaudeNestWebApplicationFactory.HawkPlanId,
            SubscriptionStatus = subscriptionStatus
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var user = new User
        {
            Auth0UserId = testUser.Auth0UserId!,
            Email = testUser.Email!,
            DisplayName = testUser.Name,
            AccountId = account.Id,
            IsAdmin = isAdmin
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (user, account);
    }

    public static async Task<Agent> SeedAgentAsync(
        IServiceProvider services,
        Guid accountId,
        string name = "Test Agent",
        bool isOnline = false)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();

        var agent = new Agent
        {
            AccountId = accountId,
            Name = name,
            Hostname = "test-host",
            OS = "linux",
            IsOnline = isOnline
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        return agent;
    }

    public static async Task<AgentCredential> SeedCredentialAsync(
        IServiceProvider services,
        Guid agentId,
        string secret = "test-secret")
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();

        var secretHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        var credential = new AgentCredential
        {
            AgentId = agentId,
            SecretHash = secretHash
        };
        db.AgentCredentials.Add(credential);
        await db.SaveChangesAsync();

        return credential;
    }

    public static async Task<Session> SeedSessionAsync(
        IServiceProvider services,
        Guid agentId,
        string state = "Running",
        string path = "/test/path")
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();

        var session = new Session
        {
            AgentId = agentId,
            Path = path,
            State = state,
            StartedAt = DateTimeOffset.UtcNow
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        return session;
    }

    public static async Task<Coupon> SeedCouponAsync(
        IServiceProvider services,
        Guid createdByUserId,
        Guid planId,
        string code = "TEST-COUPON",
        int freeMonths = 1,
        int maxRedemptions = 100,
        bool isActive = true)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();

        var coupon = new Coupon
        {
            Code = code,
            PlanId = planId,
            FreeMonths = freeMonths,
            MaxRedemptions = maxRedemptions,
            IsActive = isActive,
            CreatedByUserId = createdByUserId
        };
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();

        return coupon;
    }

    public static async Task<CompanyDeal> SeedCompanyDealAsync(
        IServiceProvider services,
        Guid createdByUserId,
        Guid planId,
        string domain = "acme.com")
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();

        var deal = new CompanyDeal
        {
            Domain = domain,
            PlanId = planId,
            CreatedByUserId = createdByUserId
        };
        db.CompanyDeals.Add(deal);
        await db.SaveChangesAsync();

        return deal;
    }

    public static async Task<CouponRedemption> SeedCouponRedemptionAsync(
        IServiceProvider services,
        Guid couponId,
        Guid accountId,
        DateTimeOffset freeUntil)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();

        var redemption = new CouponRedemption
        {
            CouponId = couponId,
            AccountId = accountId,
            FreeUntil = freeUntil
        };
        db.CouponRedemptions.Add(redemption);
        await db.SaveChangesAsync();

        return redemption;
    }

    public static async Task<AccountLedger> SeedLedgerEntryAsync(
        IServiceProvider services,
        Guid accountId,
        LedgerEntryType entryType = LedgerEntryType.PaymentReceived,
        int amountCents = 500,
        string description = "Test entry")
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();

        var entry = new AccountLedger
        {
            AccountId = accountId,
            EntryType = entryType,
            AmountCents = amountCents,
            Description = description,
            PlanId = ClaudeNestWebApplicationFactory.HawkPlanId
        };
        db.AccountLedger.Add(entry);
        await db.SaveChangesAsync();

        return entry;
    }

    public static async Task<UserFolderPreference> SeedFolderPreferenceAsync(
        IServiceProvider services,
        Guid userId,
        Guid agentId,
        string path = "/test/path",
        bool isFavorite = true,
        string? color = null)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();

        var now = DateTimeOffset.UtcNow;
        var pref = new UserFolderPreference
        {
            UserId = userId,
            AgentId = agentId,
            Path = path,
            IsFavorite = isFavorite,
            Color = color,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.UserFolderPreferences.Add(pref);
        await db.SaveChangesAsync();

        return pref;
    }

    public static async Task<PairingToken> SeedPairingTokenAsync(
        IServiceProvider services,
        Guid userId,
        string token,
        DateTimeOffset expiresAt,
        bool redeemed = false)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();

        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var pairingToken = new PairingToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            RedeemedAt = redeemed ? DateTimeOffset.UtcNow : null
        };
        db.PairingTokens.Add(pairingToken);
        await db.SaveChangesAsync();

        return pairingToken;
    }
}
