using ClaudeNest.Backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaudeNest.Backend.Data.EntityConfigurations;

public class AgentCredentialConfiguration : IEntityTypeConfiguration<AgentCredential>
{
    public void Configure(EntityTypeBuilder<AgentCredential> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
        entity.Property(e => e.SecretHash).HasMaxLength(64).IsRequired();
        entity.Property(e => e.IssuedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasOne(e => e.Agent).WithMany(a => a.Credentials).HasForeignKey(e => e.AgentId);
    }
}
