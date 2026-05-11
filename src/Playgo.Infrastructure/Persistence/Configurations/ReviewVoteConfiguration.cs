using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Playgo.Domain.Entities;

namespace Playgo.Infrastructure.Persistence.Configurations;

public class ReviewVoteConfiguration : IEntityTypeConfiguration<ReviewVote>
{
    public void Configure(EntityTypeBuilder<ReviewVote> builder)
    {
        builder.ToTable("review_votes");
        builder.HasKey(rv => rv.Id);

        builder.Property(rv => rv.VoteType).HasConversion<int>();

        // Partial unique: one ACTIVE vote per (review, user). Soft-deleted votes excluded.
        builder.HasIndex(rv => new { rv.ReviewId, rv.UserId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasOne(rv => rv.Review)
            .WithMany(r => r.Votes)
            .HasForeignKey(rv => rv.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rv => rv.User)
            .WithMany()
            .HasForeignKey(rv => rv.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
