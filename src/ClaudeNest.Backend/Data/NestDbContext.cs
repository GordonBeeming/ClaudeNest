using ClaudeNest.Backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Data;

public class NestDbContext(DbContextOptions<NestDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentCredential> AgentCredentials => Set<AgentCredential>();
    public DbSet<PairingToken> PairingTokens => Set<PairingToken>();
    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.Auth0UserId).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.Auth0UserId).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(256);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.Hostname).HasMaxLength(256);
            entity.Property(e => e.OS).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(e => e.User).WithMany(u => u.Agents).HasForeignKey(e => e.UserId);
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
