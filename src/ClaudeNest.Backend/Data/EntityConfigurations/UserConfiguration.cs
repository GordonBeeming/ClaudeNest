using ClaudeNest.Backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaudeNest.Backend.Data.EntityConfigurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
        entity.Property(e => e.Auth0UserId).HasMaxLength(128).IsRequired();
        entity.HasIndex(e => e.Auth0UserId).IsUnique();
        entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
        entity.Property(e => e.DisplayName).HasMaxLength(256);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasOne(e => e.Account).WithMany(a => a.Users).HasForeignKey(e => e.AccountId);
    }
}
