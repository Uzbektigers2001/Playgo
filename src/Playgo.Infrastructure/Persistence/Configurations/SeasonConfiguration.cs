using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Playgo.Domain.Entities;

namespace Playgo.Infrastructure.Persistence.Configurations;

public class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.ToTable("seasons");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title).HasMaxLength(200);
        builder.Property(s => s.PosterUrl).HasMaxLength(1000);

        builder.HasOne(s => s.Content)
            .WithMany(c => c.Seasons)
            .HasForeignKey(s => s.ContentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.ContentId, s.SeasonNumber });
    }
}
