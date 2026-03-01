using System.Security.Cryptography;
using System.Text;
using ClaudeNest.Backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Data;

public static class DevDataSeeder
{
    // Well-known dev identifiers — deterministic so AppHost can pass them to Agent
    public static readonly Guid DevUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid DevAgentId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public const string DevAgentSecret = "dev-secret-do-not-use-in-production";
    public const string DevAuth0UserId = "dev|local-user";

    public static async Task SeedAsync(NestDbContext db)
    {
        // Ensure database schema exists (use EnsureCreated for dev; migrations for prod)
        await db.Database.EnsureCreatedAsync();

        // Seed dev user if not exists
        if (!await db.Users.AnyAsync(u => u.Id == DevUserId))
        {
            db.Users.Add(new User
            {
                Id = DevUserId,
                Auth0UserId = DevAuth0UserId,
                Email = "dev@localhost",
                DisplayName = "Local Developer"
            });
            await db.SaveChangesAsync();
        }

        // Seed dev agent if not exists
        if (!await db.Agents.AnyAsync(a => a.Id == DevAgentId))
        {
            db.Agents.Add(new Agent
            {
                Id = DevAgentId,
                UserId = DevUserId,
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
