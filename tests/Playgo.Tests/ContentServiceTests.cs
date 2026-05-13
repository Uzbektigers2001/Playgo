using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Content;
using Playgo.Application.Services;
using Playgo.Domain.Entities;
using Playgo.Domain.Enums;
using Playgo.Infrastructure.Persistence;
using Playgo.Tests.Common;

namespace Playgo.Tests;

public class ContentServiceTests
{
    private static (ContentService Svc, ApplicationDbContext Db) Build()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"content-{Guid.NewGuid()}")
            .Options;
        var db = new ApplicationDbContext(options);

        var loc = new Mock<ILocalizationContext>();
        loc.SetupGet(l => l.CurrentLanguage).Returns("en");

        var svc = new ContentService(db, loc.Object);
        return (svc, db);
    }

    [Fact]
    public async Task Create_AssignsSlugAndGenres()
    {
        var (svc, db) = Build();
        var genre = TestDataBuilder.CreateGenre("Action");
        db.Genres.Add(genre);
        await db.SaveChangesAsync();

        var result = await svc.CreateAsync(new CreateContentRequest(
            Title: "Hello World",
            OriginalTitle: null,
            Description: "Desc",
            ShortDescription: null,
            Type: ContentType.Movie,
            ReleaseYear: 2025,
            ReleaseDate: null,
            DurationMinutes: 100,
            Country: "UZ", Language: "uz", AgeRating: "PG",
            PosterUrl: null, BackdropUrl: null, TrailerUrl: null, VideoUrl: "x", HlsManifestUrl: null,
            Director: "Me", Cast: "Actor",
            IsFeatured: false,
            GenreIds: new List<Guid> { genre.Id }));

        result.Success.Should().BeTrue();
        result.Data!.Slug.Should().Be("hello-world");
        result.Data.Genres.Should().ContainSingle(g => g.Id == genre.Id);
    }

    [Fact]
    public async Task GetContents_FiltersByType()
    {
        var (svc, db) = Build();
        db.Contents.AddRange(
            TestDataBuilder.CreateContent("Movie 1", ContentType.Movie),
            TestDataBuilder.CreateContent("Anime 1", ContentType.Anime));
        await db.SaveChangesAsync();

        var result = await svc.GetContentsAsync(new ContentFilterRequest { Type = ContentType.Anime });

        result.Items.Should().OnlyContain(i => i.Type == ContentType.Anime);
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsFailure()
    {
        var (svc, _) = Build();
        var result = await svc.GetContentByIdAsync(Guid.NewGuid());
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetTrending_OrdersByViewCountDesc()
    {
        var (svc, db) = Build();
        var low = TestDataBuilder.CreateContent("Low");
        low.ViewCount = 5;
        var high = TestDataBuilder.CreateContent("High");
        high.ViewCount = 100;
        db.Contents.AddRange(low, high);
        await db.SaveChangesAsync();

        var items = await svc.GetTrendingAsync(10);

        items.First().Title.Should().Be("High");
    }

    [Fact]
    public async Task IncrementView_BumpsCount_AndLogsRow()
    {
        var (svc, db) = Build();
        var c = TestDataBuilder.CreateContent("ViewMe");
        db.Contents.Add(c);
        await db.SaveChangesAsync();

        await svc.IncrementViewCountAsync(c.Id);

        var fresh = await db.Contents.SingleAsync();
        fresh.ViewCount.Should().Be(1);
        (await db.ContentViewLogs.AnyAsync(l => l.ContentId == c.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_SoftDeletes()
    {
        var (svc, db) = Build();
        var c = TestDataBuilder.CreateContent("Bye");
        db.Contents.Add(c);
        await db.SaveChangesAsync();

        var result = await svc.DeleteAsync(c.Id);

        result.Success.Should().BeTrue();
        var raw = await db.Contents.IgnoreQueryFilters().SingleAsync();
        raw.IsDeleted.Should().BeTrue();
        raw.Status.Should().Be(ContentStatus.Archived);
    }
}
