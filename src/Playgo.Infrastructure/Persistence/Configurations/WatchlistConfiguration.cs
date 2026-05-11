using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Playgo.Domain.Entities;

namespace Playgo.Infrastructure.Persistence.Configurations;

public class WatchlistConfiguration : IEntityTypeConfiguration<Watchlist>
{
    public void Configure(EntityTypeBuilder<Watchlist> builder)
    {
        builder.ToTable("watchlists");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Note).HasMaxLength(500);

        builder.HasIndex(w => w.UserId);

        // Partial unique: only one ACTIVE row per (user, content). Soft-deleted rows excluded.
        builder.HasIndex(w => new { w.UserId, w.ContentId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasOne(w => w.User)
            .WithMany(u => u.Watchlist)
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(w => w.Content)
            .WithMany()
            .HasForeignKey(w => w.ContentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
