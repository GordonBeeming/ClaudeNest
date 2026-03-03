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
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountLedger> AccountLedger => Set<AccountLedger>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();
    public DbSet<CompanyDeal> CompanyDeals => Set<CompanyDeal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NestDbContext).Assembly);

        // Never use cascade delete — all deletes must be explicit
        foreach (var relationship in modelBuilder.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.NoAction;
        }
    }
}
