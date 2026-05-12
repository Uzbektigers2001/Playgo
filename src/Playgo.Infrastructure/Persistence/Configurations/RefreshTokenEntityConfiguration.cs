using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Playgo.Domain.Entities;

namespace Playgo.Infrastructure.Persistence.Configurations;

public class RefreshTokenEntityConfiguration : IEntityTypeConfiguration<RefreshTokenEntity>
{
    public void Configure(EntityTypeBuilder<RefreshTokenEntity> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Token).IsRequired().HasMaxLength(500);
        builder.HasIndex(t => t.Token).IsUnique();

        builder.Property(t => t.CreatedByIp).IsRequired().HasMaxLength(45);
        builder.Property(t => t.RevokedByIp).HasMaxLength(45);
        builder.Property(t => t.ReplacedByToken).HasMaxLength(500);
        builder.Property(t => t.UserAgent).HasMaxLength(500);

        builder.HasIndex(t => t.UserId);

        builder.HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
