using ClaudeNest.Backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaudeNest.Backend.Data.EntityConfigurations;

public class AccountLedgerConfiguration : IEntityTypeConfiguration<AccountLedger>
{
    public void Configure(EntityTypeBuilder<AccountLedger> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
        entity.Property(e => e.EntryType).HasMaxLength(32).HasConversion<string>();
        entity.Property(e => e.Currency).HasMaxLength(8).HasDefaultValue("aud");
        entity.Property(e => e.Description).HasMaxLength(512).IsRequired();
        entity.Property(e => e.StripeInvoiceId).HasMaxLength(256);
        entity.Property(e => e.StripePaymentIntentId).HasMaxLength(256);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(e => new { e.AccountId, e.CreatedAt });
        entity.HasOne(e => e.Account).WithMany().HasForeignKey(e => e.AccountId);
        entity.HasOne(e => e.Plan).WithMany().HasForeignKey(e => e.PlanId).IsRequired(false);
        entity.HasOne(e => e.Coupon).WithMany().HasForeignKey(e => e.CouponId).IsRequired(false);
        entity.HasOne(e => e.CompanyDeal).WithMany().HasForeignKey(e => e.CompanyDealId).IsRequired(false);
    }
}
