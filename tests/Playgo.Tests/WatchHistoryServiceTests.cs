using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Playgo.Application.DTOs.Content;
using Playgo.Application.Services;
using Playgo.Domain.Entities;
using Playgo.Infrastructure.Persistence;
using Playgo.Tests.Common;

namespace Playgo.Tests;

public class WatchHistoryServiceTests
{
    private static (WatchHistoryService Svc, ApplicationDbContext Db, User User, Content Content) Build()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"watch-{Guid.NewGuid()}").Options;
        var db = new ApplicationDbContext(options);

        var user = TestDataBuilder.CreateUser();
        var content = TestDataBuilder.CreateContent("Movie");
        db.Users.Add(user);
        db.Contents.Add(content);
        db.SaveChanges();

        return (new WatchHistoryService(db), db, user, content);
    }

    [Fact]
    public async Task UpdateProgress_CreatesRow()
    {
        var (svc, db, user, content) = Build();

        var result = await svc.UpdateProgressAsync(user.Id, new UpdateWatchProgressRequest(content.Id, null, 30, 100));

        result.Success.Should().BeTrue();
        var row = await db.WatchHistories.SingleAsync();
        row.PositionSeconds.Should().Be(30);
        row.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateProgress_AtCompletion_MarksCompleted()
    {
        var (svc, db, user, content) = Build();

        await svc.UpdateProgressAsync(user.Id, new UpdateWatchProgressRequest(content.Id, null, 95, 100));

        var row = await db.WatchHistories.SingleAsync();
        row.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateProgress_Existing_Updates()
    {
        var (svc, db, user, content) = Build();
        await svc.UpdateProgressAsync(user.Id, new UpdateWatchProgressRequest(content.Id, null, 30, 100));

        await svc.UpdateProgressAsync(user.Id, new UpdateWatchProgressRequest(content.Id, null, 60, 100));

        (await db.WatchHistories.CountAsync()).Should().Be(1);
        var row = await db.WatchHistories.SingleAsync();
        row.PositionSeconds.Should().Be(60);
    }

    [Fact]
    public async Task ContinueWatching_ExcludesCompletedAndShortPlays()
    {
        var (svc, db, user, content) = Build();
        var c2 = TestDataBuilder.CreateContent("Other");
        db.Contents.Add(c2);
        await db.SaveChangesAsync();

        // 1) In progress — should appear
        await svc.UpdateProgressAsync(user.Id, new UpdateWatchProgressRequest(content.Id, null, 30, 100));
        // 2) Completed — should NOT appear
        await svc.UpdateProgressAsync(user.Id, new UpdateWatchProgressRequest(c2.Id, null, 95, 100));

        var items = await svc.GetContinueWatchingAsync(user.Id, 10);

        items.Should().ContainSingle();
        items.Single().ContentId.Should().Be(content.Id);
    }

    [Fact]
    public async Task ClearHistory_SoftDeletesAll()
    {
        var (svc, db, user, content) = Build();
        await svc.UpdateProgressAsync(user.Id, new UpdateWatchProgressRequest(content.Id, null, 10, 100));

        await svc.ClearHistoryAsync(user.Id);

        (await db.WatchHistories.IgnoreQueryFilters().CountAsync(w => w.UserId == user.Id && w.IsDeleted)).Should().Be(1);
        (await db.WatchHistories.AnyAsync(w => w.UserId == user.Id)).Should().BeFalse();
    }
}
