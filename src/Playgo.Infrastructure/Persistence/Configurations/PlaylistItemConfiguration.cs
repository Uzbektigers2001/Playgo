using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Playgo.Domain.Entities;

namespace Playgo.Infrastructure.Persistence.Configurations;

public class PlaylistItemConfiguration : IEntityTypeConfiguration<PlaylistItem>
{
    public void Configure(EntityTypeBuilder<PlaylistItem> builder)
    {
        builder.ToTable("playlist_items");
        builder.HasKey(pi => pi.Id);

        builder.HasIndex(pi => new { pi.PlaylistId, pi.OrderIndex });

        // Partial unique: a given content appears at most once per playlist (excluding soft-deleted rows).
        builder.HasIndex(pi => new { pi.PlaylistId, pi.ContentId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasOne(pi => pi.Playlist)
            .WithMany(p => p.Items)
            .HasForeignKey(pi => pi.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pi => pi.Content)
            .WithMany()
            .HasForeignKey(pi => pi.ContentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
