using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Playgo.Domain.Entities;

namespace Playgo.Infrastructure.Persistence.Configurations;

public class WatchHistoryConfiguration : IEntityTypeConfiguration<WatchHistory>
{
    public void Configure(EntityTypeBuilder<WatchHistory> builder)
    {
        builder.ToTable("watch_history");
        builder.HasKey(w => w.Id);

        builder.HasIndex(w => new { w.UserId, w.ContentId, w.EpisodeId }).IsUnique();

        builder.HasIndex(w => new { w.UserId, w.LastWatchedAt })
            .IsDescending(false, true);

        builder.HasOne(w => w.User)
            .WithMany(u => u.WatchHistories)
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.Content)
            .WithMany(c => c.WatchHistories)
            .HasForeignKey(w => w.ContentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.Episode)
            .WithMany()
            .HasForeignKey(w => w.EpisodeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
