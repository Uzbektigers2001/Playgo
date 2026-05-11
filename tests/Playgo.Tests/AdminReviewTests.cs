using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Playgo.Application.Services;
using Playgo.Domain.Entities;
using Playgo.Domain.Enums;
using Playgo.Infrastructure.Persistence;

namespace Playgo.Tests;

public class AdminReviewTests
{
    private static (AdminReviewService Service, ApplicationDbContext Db, Review Review) BuildSubject(bool approved = true)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"admin-{Guid.NewGuid()}")
            .Options;
        var db = new ApplicationDbContext(options);

        var user = new User { Username = "u", Email = "u@t.com", PasswordHash = "h", Role = UserRole.User };
        var content = new Content
        {
            Title = "Movie", Slug = "movie", Description = "d", Type = ContentType.Movie, Status = ContentStatus.Published,
            RatingCount = approved ? 1 : 0,
            AverageRating = approved ? 7 : 0,
        };
        var review = new Review
        {
            UserId = user.Id,
            ContentId = content.Id,
            Rating = 7,
            Comment = "ok",
            IsApproved = approved,
        };
        db.Users.Add(user);
        db.Contents.Add(content);
        db.Reviews.Add(review);
        db.SaveChanges();

        return (new AdminReviewService(db), db, review);
    }

    [Fact]
    public async Task ApproveReview_SetsIsApprovedTrue()
    {
        var (service, db, review) = BuildSubject(approved: false);

        var result = await service.ApproveAsync(review.Id);

        result.Success.Should().BeTrue();
        var stored = await db.Reviews.AsNoTracking().SingleAsync(r => r.Id == review.Id);
        stored.IsApproved.Should().BeTrue();
        stored.RejectionReason.Should().BeNull();

        // Approving a previously-pending review folds the rating into the content average.
        var content = await db.Contents.AsNoTracking().SingleAsync(c => c.Id == review.ContentId);
        content.RatingCount.Should().Be(1);
        content.AverageRating.Should().Be(7);
    }

    [Fact]
    public async Task RejectReview_StoresReason()
    {
        var (service, db, review) = BuildSubject(approved: true);

        var result = await service.RejectAsync(review.Id, reason: "spam");

        result.Success.Should().BeTrue();
        var stored = await db.Reviews.AsNoTracking().SingleAsync(r => r.Id == review.Id);
        stored.IsApproved.Should().BeFalse();
        stored.RejectionReason.Should().Be("spam");

        // Rejection of an approved review pulls its rating out of the aggregate.
        var content = await db.Contents.AsNoTracking().SingleAsync(c => c.Id == review.ContentId);
        content.RatingCount.Should().Be(0);
        content.AverageRating.Should().Be(0);
    }

    [Fact]
    public async Task DeleteReview_SoftDeletes()
    {
        var (service, db, review) = BuildSubject(approved: true);

        var result = await service.DeleteAsync(review.Id);

        result.Success.Should().BeTrue();
        var stored = await db.Reviews.IgnoreQueryFilters().AsNoTracking().SingleAsync(r => r.Id == review.Id);
        stored.IsDeleted.Should().BeTrue();

        var visible = await db.Reviews.AsNoTracking().AnyAsync(r => r.Id == review.Id);
        visible.Should().BeFalse();
    }
}
