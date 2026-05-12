using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Playgo.Domain.Entities;

namespace Playgo.Infrastructure.Persistence.Configurations;

public class ContentViewLogConfiguration : IEntityTypeConfiguration<ContentViewLog>
{
    public void Configure(EntityTypeBuilder<ContentViewLog> builder)
    {
        builder.ToTable("content_view_logs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.Property(x => x.UserAgent).HasMaxLength(500);

        builder.HasIndex(x => x.ContentId);
        builder.HasIndex(x => x.ViewedAt);

        builder.HasOne(x => x.Content)
            .WithMany()
            .HasForeignKey(x => x.ContentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
