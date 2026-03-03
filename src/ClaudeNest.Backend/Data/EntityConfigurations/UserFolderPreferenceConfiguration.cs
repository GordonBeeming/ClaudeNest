using ClaudeNest.Backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaudeNest.Backend.Data.EntityConfigurations;

public class UserFolderPreferenceConfiguration : IEntityTypeConfiguration<UserFolderPreference>
{
    public void Configure(EntityTypeBuilder<UserFolderPreference> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
        entity.Property(e => e.Path).HasMaxLength(1024).IsRequired();
        entity.Property(e => e.Color).HasMaxLength(16);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasIndex(e => new { e.UserId, e.AgentId, e.Path }).IsUnique();
        entity.HasIndex(e => new { e.UserId, e.AgentId });

        entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        entity.HasOne(e => e.Agent).WithMany().HasForeignKey(e => e.AgentId);
    }
}
