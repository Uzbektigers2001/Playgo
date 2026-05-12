using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Admin;
using Playgo.Application.Services;
using Playgo.Domain.Entities;
using Playgo.Domain.Enums;
using Playgo.Infrastructure.Persistence;

namespace Playgo.Tests;

public class AdminDashboardServiceTests
{
    private static ApplicationDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"dashboard-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void Seed(ApplicationDbContext db)
    {
        var u = new User { Username = "u", Email = "u@x.com", PasswordHash = "h" };
        var c = new Content
        {
            Title = "M", Slug = "m", Description = "d", Type = ContentType.Movie,
            Status = ContentStatus.Published, ViewCount = 5, AverageRating = 7, RatingCount = 1,
        };
        db.Users.Add(u);
        db.Contents.Add(c);
        db.ContentViewLogs.Add(new ContentViewLog
        {
            ContentId = c.Id,
            ViewedAt = DateTime.UtcNow.Date,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task GetStats_ReturnsAggregateNumbers()
    {
        var db = NewDb();
        Seed(db);
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetAsync<AdminDashboardStatsDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminDashboardStatsDto?)null);

        var service = new AdminDashboardService(db, cache.Object);
        var stats = await service.GetStatsAsync();

        stats.TotalUsers.Should().Be(1);
        stats.TotalContents.Should().Be(1);
        stats.PublishedContents.Should().Be(1);
        stats.TotalViews.Should().Be(5);
        stats.AverageRating.Should().Be(7);
        stats.RecentMovies.Should().HaveCount(1);
        stats.RecentUsers.Should().HaveCount(1);
        stats.ViewsLast30Days.Should().HaveCount(30);
        cache.Verify(c => c.SetAsync(
            "admin:dashboard:stats",
            It.IsAny<AdminDashboardStatsDto>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStats_ReturnsCachedOnSecondCall_WithoutHittingDb()
    {
        var db = NewDb();
        Seed(db);
        var cached = new AdminDashboardStatsDto(
            42, 7, 0, 0, 0, 0, 0, 0, 0, 0,
            new List<AdminRecentItemDto>(),
            new List<AdminRecentItemDto>(),
            new List<AdminGenreStatDto>(),
            new List<AdminViewsByDayDto>());

        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetAsync<AdminDashboardStatsDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var service = new AdminDashboardService(db, cache.Object);
        var stats = await service.GetStatsAsync();

        stats.Should().BeSameAs(cached);
        stats.TotalUsers.Should().Be(42);
        cache.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<AdminDashboardStatsDto>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
