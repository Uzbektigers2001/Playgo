using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Playgo.Application.Services;
using Playgo.Domain.Entities;
using Playgo.Infrastructure.Persistence;
using Playgo.Tests.Common;

namespace Playgo.Tests;

public class FavoriteServiceTests
{
    private static (FavoriteService Svc, ApplicationDbContext Db, User User, Content Content) Build()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"fav-{Guid.NewGuid()}").Options;
        var db = new ApplicationDbContext(options);

        var user = TestDataBuilder.CreateUser();
        var content = TestDataBuilder.CreateContent("Sample");
        db.Users.Add(user);
        db.Contents.Add(content);
        db.SaveChanges();

        return (new FavoriteService(db), db, user, content);
    }

    [Fact]
    public async Task Add_ThenIsFavorite_ReturnsTrue()
    {
        var (svc, _, user, content) = Build();

        var add = await svc.AddAsync(user.Id, content.Id);
        add.Success.Should().BeTrue();

        var isFav = await svc.IsFavoriteAsync(user.Id, content.Id);
        isFav.Should().BeTrue();
    }

    [Fact]
    public async Task Add_UnknownContent_Fails()
    {
        var (svc, _, user, _) = Build();

        var result = await svc.AddAsync(user.Id, Guid.NewGuid());

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Add_Duplicate_Idempotent()
    {
        var (svc, db, user, content) = Build();

        await svc.AddAsync(user.Id, content.Id);
        await svc.AddAsync(user.Id, content.Id);

        (await db.Favorites.CountAsync(f => f.UserId == user.Id && f.ContentId == content.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Remove_SoftDeletes_AndIsFavoriteFalse()
    {
        var (svc, _, user, content) = Build();
        await svc.AddAsync(user.Id, content.Id);

        await svc.RemoveAsync(user.Id, content.Id);

        (await svc.IsFavoriteAsync(user.Id, content.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task GetUserFavorites_PagedResults()
    {
        var (svc, db, user, _) = Build();
        for (var i = 0; i < 25; i++)
        {
            var c = TestDataBuilder.CreateContent($"Movie {i}");
            db.Contents.Add(c);
            db.Favorites.Add(new Favorite { UserId = user.Id, ContentId = c.Id });
        }
        await db.SaveChangesAsync();

        var page1 = await svc.GetUserFavoritesAsync(user.Id, 1, 10);
        var page3 = await svc.GetUserFavoritesAsync(user.Id, 3, 10);

        page1.Items.Should().HaveCount(10);
        page1.TotalCount.Should().Be(25);
        page3.Items.Should().HaveCount(5);
    }
}
