using System.Security.Cryptography;
using System.Text;
using ClaudeNest.Backend.Data.Entities;
using ClaudeNest.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Data;

public static class DevDataSeeder
{
    // Well-known dev identifiers — deterministic so AppHost can pass them to Agent
    public static readonly Guid DevUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid DevAgentId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid DevAccountId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public static readonly Guid HawkPlanId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public const string DevAgentSecret = "dev-secret-do-not-use-in-production";
    public const string DevAuth0UserId = "dev|local-user";

    public static async Task SeedAsync(NestDbContext db)
    {
        // Apply pending migrations (works for both fresh DBs and schema updates)
        await db.Database.MigrateAsync();

        // Seed plans if they don't exist (for EnsureCreatedAsync path — HasData only works with migrations)
        if (!await db.Plans.AnyAsync())
        {
            db.Plans.AddRange(
                new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Name = "Wren", MaxAgents = 1, MaxSessions = 2, PriceCents = 100, TrialDays = 14, IsActive = true, SortOrder = 1 },
                new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Name = "Robin", MaxAgents = 2, MaxSessions = 5, PriceCents = 200, TrialDays = 0, IsActive = true, SortOrder = 2 },
                new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Name = "Hawk", MaxAgents = 3, MaxSessions = 10, PriceCents = 500, TrialDays = 0, IsActive = true, SortOrder = 3 },
                new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), Name = "Eagle", MaxAgents = 5, MaxSessions = 25, PriceCents = 1000, TrialDays = 0, IsActive = true, SortOrder = 4 },
                new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000005"), Name = "Falcon", MaxAgents = 10, MaxSessions = 50, PriceCents = 2000, TrialDays = 0, IsActive = true, SortOrder = 5 },
                new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000006"), Name = "Condor", MaxAgents = 25, MaxSessions = 100, PriceCents = 5000, TrialDays = 0, IsActive = true, SortOrder = 6 }
            );
            await db.SaveChangesAsync();
        }

        // Seed dev account if not exists
        if (!await db.Accounts.AnyAsync(a => a.Id == DevAccountId))
        {
            db.Accounts.Add(new Account
            {
                Id = DevAccountId,
                Name = "Dev Account",
                PlanId = HawkPlanId,
                SubscriptionStatus = SubscriptionStatus.Active
            });
            await db.SaveChangesAsync();
        }

        // Seed dev user if not exists
        if (!await db.Users.AnyAsync(u => u.Id == DevUserId))
        {
            db.Users.Add(new User
            {
                Id = DevUserId,
                Auth0UserId = DevAuth0UserId,
                Email = "dev@localhost",
                DisplayName = "Local Developer",
                AccountId = DevAccountId
            });
            await db.SaveChangesAsync();
        }

        // Seed dev agent if not exists
        if (!await db.Agents.AnyAsync(a => a.Id == DevAgentId))
        {
            db.Agents.Add(new Agent
            {
                Id = DevAgentId,
                AccountId = DevAccountId,
                Name = "Local Dev Agent",
                Hostname = Environment.MachineName,
                OS = OperatingSystem.IsWindows() ? "windows"
                    : OperatingSystem.IsMacOS() ? "macos"
                    : "linux"
            });
            await db.SaveChangesAsync();
        }

        // Seed dev credential if not exists
        var secretHash = SHA256.HashData(Encoding.UTF8.GetBytes(DevAgentSecret));
        if (!await db.AgentCredentials.AnyAsync(c => c.AgentId == DevAgentId && c.RevokedAt == null))
        {
            db.AgentCredentials.Add(new AgentCredential
            {
                AgentId = DevAgentId,
                SecretHash = secretHash
            });
            await db.SaveChangesAsync();
        }
    }
}
