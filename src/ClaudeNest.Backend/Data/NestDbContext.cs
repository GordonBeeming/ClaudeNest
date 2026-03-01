using ClaudeNest.Backend.Data.Entities;
using ClaudeNest.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Data;

public class NestDbContext(DbContextOptions<NestDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentCredential> AgentCredentials => Set<AgentCredential>();
    public DbSet<PairingToken> PairingTokens => Set<PairingToken>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Plan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.Name).HasMaxLength(64).IsRequired();
            entity.Property(e => e.StripeProductId).HasMaxLength(256);

            entity.HasData(
                new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Name = "Wren", MaxAgents = 1, MaxSessions = 2, PriceCents = 100, TrialDays = 14, IsActive = true, SortOrder = 1 },
                new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Name = "Robin", MaxAgents = 2, MaxSessions = 5, PriceCents = 200, TrialDays = 0, IsActive = true, SortOrder = 2 },
                new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Name = "Hawk", MaxAgents = 3, MaxSessions = 10, PriceCents = 500, TrialDays = 0, IsActive = true, SortOrder = 3 },
                new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), Name = "Eagle", MaxAgents = 5, MaxSessions = 25, PriceCents = 1000, TrialDays = 0, IsActive = true, SortOrder = 4 },
                new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000005"), Name = "Falcon", MaxAgents = 10, MaxSessions = 50, PriceCents = 2000, TrialDays = 0, IsActive = true, SortOrder = 5 },
                new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000006"), Name = "Condor", MaxAgents = 25, MaxSessions = 100, PriceCents = 5000, TrialDays = 0, IsActive = true, SortOrder = 6 }
            );
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.SubscriptionStatus).HasMaxLength(32).HasConversion<string>();
            entity.Property(e => e.StripeCustomerId).HasMaxLength(256);
            entity.Property(e => e.StripeSubscriptionId).HasMaxLength(256);
            entity.Property(e => e.PermissionMode).HasMaxLength(32).HasDefaultValue("default");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(e => e.Plan).WithMany().HasForeignKey(e => e.PlanId).IsRequired(false);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.Auth0UserId).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.Auth0UserId).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(256);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(e => e.Account).WithMany(a => a.Users).HasForeignKey(e => e.AccountId);
        });

        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.Hostname).HasMaxLength(256);
            entity.Property(e => e.OS).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(e => e.Account).WithMany(a => a.Agents).HasForeignKey(e => e.AccountId);
        });

        modelBuilder.Entity<AgentCredential>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.SecretHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.IssuedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(e => e.Agent).WithMany(a => a.Credentials).HasForeignKey(e => e.AgentId);
        });

        modelBuilder.Entity<PairingToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasOne(e => e.User).WithMany(u => u.PairingTokens).HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.Path).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.State).HasMaxLength(32).IsRequired();
            entity.Property(e => e.StartedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(e => e.Agent).WithMany(a => a.Sessions).HasForeignKey(e => e.AgentId);
        });
    }
}
