using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Playgo.Domain.Entities;

namespace Playgo.Infrastructure.Persistence.Configurations;

public class GenreTranslationConfiguration : IEntityTypeConfiguration<GenreTranslation>
{
    public void Configure(EntityTypeBuilder<GenreTranslation> builder)
    {
        builder.ToTable("genre_translations");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.LanguageCode).IsRequired().HasMaxLength(8);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);

        builder.HasIndex(t => new { t.GenreId, t.LanguageCode }).IsUnique();

        builder.HasOne(t => t.Genre)
            .WithMany(g => g.Translations)
            .HasForeignKey(t => t.GenreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
