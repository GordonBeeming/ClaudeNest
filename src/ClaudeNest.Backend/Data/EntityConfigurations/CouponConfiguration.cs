using ClaudeNest.Backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaudeNest.Backend.Data.EntityConfigurations;

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
        entity.Property(e => e.Code).HasMaxLength(64).IsRequired();
        entity.HasIndex(e => e.Code).IsUnique();
        entity.Property(e => e.DiscountType).HasMaxLength(32).HasConversion<string>();
        entity.Property(e => e.PercentOff).HasPrecision(5, 2);
        entity.Property(e => e.StripeCouponId).HasMaxLength(256);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasOne(e => e.Plan).WithMany().HasForeignKey(e => e.PlanId);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId);
    }
}
