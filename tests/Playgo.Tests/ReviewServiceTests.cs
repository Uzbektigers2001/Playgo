using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Content;
using Playgo.Application.Services;
using Playgo.Domain.Entities;
using Playgo.Infrastructure.Persistence;
using Playgo.Tests.Common;

namespace Playgo.Tests;

public class ReviewServiceTests
{
    private static (ReviewService Svc, ApplicationDbContext Db, User User, Content Content) Build(bool requireModeration = false)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"reviews-{Guid.NewGuid()}").Options;
        var db = new ApplicationDbContext(options);

        var user = TestDataBuilder.CreateUser();
        var content = TestDataBuilder.CreateContent("Movie");
        db.Users.Add(user);
        db.Contents.Add(content);
        db.SaveChanges();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(user.Id);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reviews:RequireModeration"] = requireModeration.ToString().ToLowerInvariant(),
            })
            .Build();

        return (new ReviewService(db, currentUser.Object, configuration), db, user, content);
    }

    [Fact]
    public async Task Create_ApprovedByDefault_FoldsRating()
    {
        var (svc, db, user, content) = Build();

        var result = await svc.CreateAsync(user.Id, new CreateReviewRequest(content.Id, 5, "Great"));

        result.Success.Should().BeTrue();
        var fresh = await db.Contents.SingleAsync();
        fresh.RatingCount.Should().Be(1);
        fresh.AverageRating.Should().Be(5);
    }

    [Fact]
    public async Task Create_ModerationOn_LeavesRatingUntouched()
    {
        var (svc, db, user, content) = Build(requireModeration: true);

        await svc.CreateAsync(user.Id, new CreateReviewRequest(content.Id, 4, "Decent"));

        var fresh = await db.Contents.SingleAsync();
        fresh.RatingCount.Should().Be(0);
        var review = await db.Reviews.SingleAsync();
        review.IsApproved.Should().BeFalse();
    }

    [Fact]
    public async Task Update_OnlyOwner_CanEdit()
    {
        var (svc, db, user, content) = Build();
        var review = TestDataBuilder.CreateReview(user.Id, content.Id, rating: 4);
        db.Reviews.Add(review);
        await db.SaveChangesAsync();

        var stranger = Guid.NewGuid();
        var result = await svc.UpdateAsync(stranger, review.Id, new UpdateReviewRequest(2, "no"));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("own");
    }

    [Fact]
    public async Task Delete_AsAdmin_AllowsDeletion()
    {
        var (svc, db, user, content) = Build();
        var review = TestDataBuilder.CreateReview(user.Id, content.Id);
        db.Reviews.Add(review);
        await db.SaveChangesAsync();

        var result = await svc.DeleteAsync(Guid.NewGuid(), review.Id, isAdmin: true);

        result.Success.Should().BeTrue();
    }
}
