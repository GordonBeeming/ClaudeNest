using ClaudeNest.Backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaudeNest.Backend.Data.EntityConfigurations;

public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
        entity.Property(e => e.Name).HasMaxLength(256);
        entity.Property(e => e.Hostname).HasMaxLength(256);
        entity.Property(e => e.OS).HasMaxLength(64);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(e => e.Version).HasMaxLength(64);
        entity.Property(e => e.Architecture).HasMaxLength(32);
        entity.HasOne(e => e.Account).WithMany(a => a.Agents).HasForeignKey(e => e.AccountId);
    }
}
