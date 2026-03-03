using ClaudeNest.Backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaudeNest.Backend.Data.EntityConfigurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
        entity.Property(e => e.Name).HasMaxLength(64).IsRequired();
        entity.Property(e => e.StripeProductId).HasMaxLength(256);
        entity.Property(e => e.StripePriceId).HasMaxLength(256);
        entity.HasOne(e => e.DefaultCoupon).WithMany().HasForeignKey(e => e.DefaultCouponId).IsRequired(false);

        entity.HasData(
            new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Name = "Wren", MaxAgents = 1, MaxSessions = 2, PriceCents = 100, IsActive = true, SortOrder = 1 },
            new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Name = "Robin", MaxAgents = 2, MaxSessions = 5, PriceCents = 200, IsActive = true, SortOrder = 2 },
            new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Name = "Hawk", MaxAgents = 3, MaxSessions = 10, PriceCents = 500, IsActive = true, SortOrder = 3 },
            new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), Name = "Eagle", MaxAgents = 5, MaxSessions = 25, PriceCents = 1000, IsActive = true, SortOrder = 4 },
            new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000005"), Name = "Falcon", MaxAgents = 10, MaxSessions = 50, PriceCents = 2000, IsActive = true, SortOrder = 5 },
            new Plan { Id = Guid.Parse("10000000-0000-0000-0000-000000000006"), Name = "Condor", MaxAgents = 25, MaxSessions = 100, PriceCents = 5000, IsActive = true, SortOrder = 6 }
        );
    }
}
