using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.Services;
using Playgo.Domain.Entities;
using Playgo.Domain.Enums;
using Playgo.Infrastructure.Persistence;

namespace Playgo.Tests;

public class ReviewVoteTests
{
    private static (ReviewService Service, ApplicationDbContext Db, User Author, User Voter, Review Review, Mock<ICurrentUserService> CurrentUser) BuildSubject()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"vote-{Guid.NewGuid()}")
            .Options;
        var db = new ApplicationDbContext(options);

        var author = new User { Username = "author", Email = "a@t.com", PasswordHash = "h", Role = UserRole.User };
        var voter = new User { Username = "voter", Email = "v@t.com", PasswordHash = "h", Role = UserRole.User };
        db.Users.AddRange(author, voter);

        var content = new Content
        {
            Title = "Movie",
            Slug = "movie",
            Description = "d",
            Type = ContentType.Movie,
            Status = ContentStatus.Published,
        };
        db.Contents.Add(content);

        var review = new Review
        {
            UserId = author.Id,
            ContentId = content.Id,
            Rating = 8,
            Comment = "Good",
            IsApproved = true,
        };
        db.Reviews.Add(review);
        db.SaveChanges();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(c => c.UserId).Returns(voter.Id);

        var configBuilder = new ConfigurationBuilder();
        var configuration = configBuilder.Build();

        var service = new ReviewService(db, currentUser.Object, configuration);
        return (service, db, author, voter, review, currentUser);
    }

    [Fact]
    public async Task Like_FirstTime_IncrementsLikes()
    {
        var (service, db, _, voter, review, _) = BuildSubject();

        var result = await service.ToggleVoteAsync(voter.Id, review.Id, ReviewVoteType.Like);

        result.Success.Should().BeTrue();
        result.Data!.LikesCount.Should().Be(1);
        result.Data.DislikesCount.Should().Be(0);
        result.Data.MyVote.Should().Be("like");

        var stored = await db.Reviews.AsNoTracking().SingleAsync(r => r.Id == review.Id);
        stored.LikesCount.Should().Be(1);
        stored.DislikesCount.Should().Be(0);

        var votes = await db.ReviewVotes.AsNoTracking().Where(rv => !rv.IsDeleted).ToListAsync();
        votes.Should().HaveCount(1);
        votes[0].VoteType.Should().Be(ReviewVoteType.Like);
    }

    [Fact]
    public async Task Like_Toggle_DecrementsLikes()
    {
        var (service, db, _, voter, review, _) = BuildSubject();
        await service.ToggleVoteAsync(voter.Id, review.Id, ReviewVoteType.Like);

        var result = await service.ToggleVoteAsync(voter.Id, review.Id, ReviewVoteType.Like);

        result.Success.Should().BeTrue();
        result.Data!.LikesCount.Should().Be(0);
        result.Data.MyVote.Should().BeNull();

        var stored = await db.Reviews.AsNoTracking().SingleAsync(r => r.Id == review.Id);
        stored.LikesCount.Should().Be(0);

        var activeVotes = await db.ReviewVotes.AsNoTracking().Where(rv => !rv.IsDeleted).CountAsync();
        activeVotes.Should().Be(0);

        var tombstones = await db.ReviewVotes.IgnoreQueryFilters().AsNoTracking().Where(rv => rv.IsDeleted).CountAsync();
        tombstones.Should().Be(1);
    }

    [Fact]
    public async Task SwitchLikeToDislike_AdjustsBothCounters()
    {
        var (service, db, _, voter, review, _) = BuildSubject();
        await service.ToggleVoteAsync(voter.Id, review.Id, ReviewVoteType.Like);

        var result = await service.ToggleVoteAsync(voter.Id, review.Id, ReviewVoteType.Dislike);

        result.Success.Should().BeTrue();
        result.Data!.LikesCount.Should().Be(0);
        result.Data.DislikesCount.Should().Be(1);
        result.Data.MyVote.Should().Be("dislike");

        var stored = await db.Reviews.AsNoTracking().SingleAsync(r => r.Id == review.Id);
        stored.LikesCount.Should().Be(0);
        stored.DislikesCount.Should().Be(1);

        var votes = await db.ReviewVotes.AsNoTracking().Where(rv => !rv.IsDeleted).ToListAsync();
        votes.Should().HaveCount(1);
        votes[0].VoteType.Should().Be(ReviewVoteType.Dislike);
    }

    [Fact]
    public async Task ReviewNotFound_Fails()
    {
        var (service, _, _, voter, _, _) = BuildSubject();

        var result = await service.ToggleVoteAsync(voter.Id, Guid.NewGuid(), ReviewVoteType.Like);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Review not found");
    }

    [Fact]
    public async Task ConcurrentVotes_RemainConsistent()
    {
        // Two distinct voters both vote Like — counters should reflect 2 likes and there should be two distinct vote rows.
        var (service, db, _, voter1, review, currentUser) = BuildSubject();

        var voter2 = new User { Username = "voter2", Email = "v2@t.com", PasswordHash = "h", Role = UserRole.User };
        db.Users.Add(voter2);
        await db.SaveChangesAsync();

        await service.ToggleVoteAsync(voter1.Id, review.Id, ReviewVoteType.Like);
        await service.ToggleVoteAsync(voter2.Id, review.Id, ReviewVoteType.Like);

        var stored = await db.Reviews.AsNoTracking().SingleAsync(r => r.Id == review.Id);
        stored.LikesCount.Should().Be(2);

        var votes = await db.ReviewVotes.AsNoTracking().Where(rv => !rv.IsDeleted).ToListAsync();
        votes.Should().HaveCount(2);
        votes.Select(v => v.UserId).Should().BeEquivalentTo(new[] { voter1.Id, voter2.Id });
    }

    [Fact]
    public async Task MyVote_ReflectsCurrentUserChoice()
    {
        var (service, db, _, voter, review, currentUser) = BuildSubject();
        await service.ToggleVoteAsync(voter.Id, review.Id, ReviewVoteType.Like);

        // As the voter:
        currentUser.Setup(c => c.UserId).Returns(voter.Id);
        var asVoter = await service.GetReviewsForContentAsync(review.ContentId, 1, 10);
        asVoter.Items.Should().ContainSingle();
        asVoter.Items.Single().MyVote.Should().Be("like");

        // As a different (unrelated) user:
        var stranger = new User { Username = "stranger", Email = "s@t.com", PasswordHash = "h", Role = UserRole.User };
        db.Users.Add(stranger);
        await db.SaveChangesAsync();
        currentUser.Setup(c => c.UserId).Returns(stranger.Id);
        var asStranger = await service.GetReviewsForContentAsync(review.ContentId, 1, 10);
        asStranger.Items.Single().MyVote.Should().BeNull();

        // As anonymous:
        currentUser.Setup(c => c.UserId).Returns((Guid?)null);
        var anon = await service.GetReviewsForContentAsync(review.ContentId, 1, 10);
        anon.Items.Single().MyVote.Should().BeNull();
    }
}
