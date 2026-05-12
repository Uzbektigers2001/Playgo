using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Playgo.Application.DTOs.UserActivity;
using Playgo.Application.Services;
using Playgo.Domain.Entities;
using Playgo.Domain.Enums;
using Playgo.Infrastructure.Persistence;

namespace Playgo.Tests;

public class WatchlistServiceTests
{
    private static (WatchlistService Service, ApplicationDbContext Db, User User, Content Content) BuildSubject(bool seedContent = true)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"watchlist-{Guid.NewGuid()}")
            .Options;
        var db = new ApplicationDbContext(options);

        var user = new User
        {
            Username = "alice",
            Email = "alice@test.com",
            PasswordHash = "hash",
            Role = UserRole.User,
        };
        db.Users.Add(user);

        var content = new Content
        {
            Title = "Movie A",
            Slug = "movie-a",
            Description = "desc",
            Type = ContentType.Movie,
            Status = ContentStatus.Published,
        };
        if (seedContent) db.Contents.Add(content);
        db.SaveChanges();

        var service = new WatchlistService(db);
        return (service, db, user, content);
    }

    [Fact]
    public async Task AddItem_Success()
    {
        var (service, db, user, content) = BuildSubject();

        var result = await service.AddAsync(user.Id, new AddToWatchlistRequest(content.Id, Priority: 5, Note: "later"));

        result.Success.Should().BeTrue();
        var row = await db.Watchlists.AsNoTracking().SingleAsync();
        row.UserId.Should().Be(user.Id);
        row.ContentId.Should().Be(content.Id);
        row.Priority.Should().Be(5);
        row.Note.Should().Be("later");
        row.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task AddDuplicate_Fails()
    {
        var (service, db, user, content) = BuildSubject();

        var first = await service.AddAsync(user.Id, new AddToWatchlistRequest(content.Id, null, null));
        first.Success.Should().BeTrue();

        var second = await service.AddAsync(user.Id, new AddToWatchlistRequest(content.Id, null, null));

        second.Success.Should().BeFalse();
        second.Error.Should().Contain("already");

        var rows = await db.Watchlists.AsNoTracking().Where(w => !w.IsDeleted).CountAsync();
        rows.Should().Be(1);
    }

    [Fact]
    public async Task RemoveItem_Success()
    {
        var (service, db, user, content) = BuildSubject();
        await service.AddAsync(user.Id, new AddToWatchlistRequest(content.Id, null, null));

        var result = await service.RemoveAsync(user.Id, content.Id);

        result.Success.Should().BeTrue();
        (await service.IsInWatchlistAsync(user.Id, content.Id)).Should().BeFalse();

        var stillThere = await db.Watchlists.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        stillThere.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserWatchlist_PaginatedCorrectly()
    {
        var (service, db, user, _) = BuildSubject(seedContent: false);

        for (var i = 0; i < 5; i++)
        {
            var c = new Content
            {
                Title = $"Movie {i}",
                Slug = $"movie-{i}",
                Description = "d",
                Type = ContentType.Movie,
                Status = ContentStatus.Published,
            };
            db.Contents.Add(c);
            await db.SaveChangesAsync();
            await service.AddAsync(user.Id, new AddToWatchlistRequest(c.Id, Priority: i, Note: null));
        }

        var page1 = await service.GetUserWatchlistAsync(user.Id, page: 1, pageSize: 2);
        var page2 = await service.GetUserWatchlistAsync(user.Id, page: 2, pageSize: 2);
        var page3 = await service.GetUserWatchlistAsync(user.Id, page: 3, pageSize: 2);

        page1.TotalCount.Should().Be(5);
        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
        page3.Items.Should().HaveCount(1);

        // Ordered by Priority ascending (lower priority sorts first)
        page1.Items.First().Priority.Should().Be(0);
        page1.Items.Last().Priority.Should().Be(1);
    }

    [Fact]
    public async Task ContentNotFound_Fails()
    {
        var (service, _, user, _) = BuildSubject(seedContent: false);

        var result = await service.AddAsync(user.Id, new AddToWatchlistRequest(Guid.NewGuid(), null, null));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Content not found");
    }
}
