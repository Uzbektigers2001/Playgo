using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Playgo.Domain.Entities;

namespace Playgo.Infrastructure.Persistence.Configurations;

public class ContentConfiguration : IEntityTypeConfiguration<Content>
{
    public void Configure(EntityTypeBuilder<Content> builder)
    {
        builder.ToTable("contents");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Slug).IsRequired().HasMaxLength(300);
        builder.HasIndex(c => c.Slug).IsUnique();

        builder.Property(c => c.Title).IsRequired().HasMaxLength(200);
        builder.Property(c => c.OriginalTitle).HasMaxLength(200);
        builder.Property(c => c.Description).IsRequired();
        builder.Property(c => c.ShortDescription).HasMaxLength(500);
        builder.Property(c => c.Country).HasMaxLength(100);
        builder.Property(c => c.Language).HasMaxLength(100);
        builder.Property(c => c.AgeRating).HasMaxLength(20);
        builder.Property(c => c.PosterUrl).HasMaxLength(1000);
        builder.Property(c => c.BackdropUrl).HasMaxLength(1000);
        builder.Property(c => c.TrailerUrl).HasMaxLength(1000);
        builder.Property(c => c.VideoUrl).HasMaxLength(1000);
        builder.Property(c => c.HlsManifestUrl).HasMaxLength(1000);
        builder.Property(c => c.Director).HasMaxLength(200);

        builder.Property(c => c.Type)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasIndex(c => new { c.Status, c.IsFeatured });
        builder.HasIndex(c => c.ViewCount);
    }
}
