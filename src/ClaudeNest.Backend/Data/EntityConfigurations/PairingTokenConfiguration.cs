using ClaudeNest.Backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaudeNest.Backend.Data.EntityConfigurations;

public class PairingTokenConfiguration : IEntityTypeConfiguration<PairingToken>
{
    public void Configure(EntityTypeBuilder<PairingToken> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
        entity.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();
        entity.HasOne(e => e.User).WithMany(u => u.PairingTokens).HasForeignKey(e => e.UserId);
    }
}
