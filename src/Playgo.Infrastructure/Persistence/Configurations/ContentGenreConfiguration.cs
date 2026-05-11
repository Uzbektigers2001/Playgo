using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Playgo.Domain.Entities;

namespace Playgo.Infrastructure.Persistence.Configurations;

public class ContentGenreConfiguration : IEntityTypeConfiguration<ContentGenre>
{
    public void Configure(EntityTypeBuilder<ContentGenre> builder)
    {
        builder.ToTable("content_genres");
        builder.HasKey(cg => new { cg.ContentId, cg.GenreId });

        builder.HasOne(cg => cg.Content)
            .WithMany(c => c.ContentGenres)
            .HasForeignKey(cg => cg.ContentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cg => cg.Genre)
            .WithMany(g => g.ContentGenres)
            .HasForeignKey(cg => cg.GenreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
