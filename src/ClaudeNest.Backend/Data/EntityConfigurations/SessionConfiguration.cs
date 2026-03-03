using ClaudeNest.Backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaudeNest.Backend.Data.EntityConfigurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
        entity.Property(e => e.Path).HasMaxLength(1024).IsRequired();
        entity.Property(e => e.State).HasMaxLength(32).IsRequired();
        entity.Property(e => e.StartedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasOne(e => e.Agent).WithMany(a => a.Sessions).HasForeignKey(e => e.AgentId);
    }
}
