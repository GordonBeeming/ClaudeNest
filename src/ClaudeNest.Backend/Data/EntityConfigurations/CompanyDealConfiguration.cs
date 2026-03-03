using ClaudeNest.Backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaudeNest.Backend.Data.EntityConfigurations;

public class CompanyDealConfiguration : IEntityTypeConfiguration<CompanyDeal>
{
    public void Configure(EntityTypeBuilder<CompanyDeal> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
        entity.Property(e => e.Domain).HasMaxLength(256).IsRequired();
        entity.HasIndex(e => e.Domain).IsUnique();
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasOne(e => e.Plan).WithMany().HasForeignKey(e => e.PlanId);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId);
    }
}
