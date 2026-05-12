using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Playgo.Domain.Entities;

namespace Playgo.Infrastructure.Persistence.Configurations;

public class ContentTranslationConfiguration : IEntityTypeConfiguration<ContentTranslation>
{
    public void Configure(EntityTypeBuilder<ContentTranslation> builder)
    {
        builder.ToTable("content_translations");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.LanguageCode).IsRequired().HasMaxLength(8);

        builder.Property(t => t.Title).IsRequired().HasMaxLength(500);
        builder.Property(t => t.OriginalTitle).HasMaxLength(500);
        builder.Property(t => t.Description).IsRequired();
        builder.Property(t => t.ShortDescription).HasMaxLength(1000);
        builder.Property(t => t.Director).HasMaxLength(300);

        builder.HasIndex(t => new { t.ContentId, t.LanguageCode }).IsUnique();

        builder.HasOne(t => t.Content)
            .WithMany(c => c.Translations)
            .HasForeignKey(t => t.ContentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
