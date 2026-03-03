using ClaudeNest.Backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaudeNest.Backend.Data.EntityConfigurations;

public class CouponRedemptionConfiguration : IEntityTypeConfiguration<CouponRedemption>
{
    public void Configure(EntityTypeBuilder<CouponRedemption> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
        entity.Property(e => e.StripeCheckoutSessionId).HasMaxLength(256);
        entity.Property(e => e.RedeemedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(e => new { e.CouponId, e.AccountId }).IsUnique();
        entity.HasOne(e => e.Coupon).WithMany(c => c.Redemptions).HasForeignKey(e => e.CouponId);
        entity.HasOne(e => e.Account).WithMany(a => a.CouponRedemptions).HasForeignKey(e => e.AccountId);
    }
}
