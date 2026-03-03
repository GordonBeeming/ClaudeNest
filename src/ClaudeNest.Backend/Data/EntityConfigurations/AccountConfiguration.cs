using ClaudeNest.Backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaudeNest.Backend.Data.EntityConfigurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
        entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
        entity.Property(e => e.SubscriptionStatus).HasMaxLength(32).HasConversion<string>();
        entity.Property(e => e.StripeCustomerId).HasMaxLength(256);
        entity.Property(e => e.StripeSubscriptionId).HasMaxLength(256);
        entity.Property(e => e.StripePaymentMethodFingerprint).HasMaxLength(256);
        entity.Property(e => e.PermissionMode).HasMaxLength(32).HasDefaultValue("default");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasOne(e => e.Plan).WithMany().HasForeignKey(e => e.PlanId).IsRequired(false);
    }
}
