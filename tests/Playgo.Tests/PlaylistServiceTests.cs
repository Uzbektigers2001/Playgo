using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Playgo.Application.DTOs.UserActivity;
using Playgo.Application.Services;
using Playgo.Domain.Entities;
using Playgo.Domain.Enums;
using Playgo.Infrastructure.Persistence;

namespace Playgo.Tests;

public class PlaylistServiceTests
{
    private static (PlaylistService Service, ApplicationDbContext Db, User Owner, User Other, Content[] Contents) BuildSubject(int contentCount = 3)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"playlist-{Guid.NewGuid()}")
            .Options;
        var db = new ApplicationDbContext(options);

        var owner = new User { Username = "owner", Email = "owner@test.com", PasswordHash = "h", Role = UserRole.User };
        var other = new User { Username = "other", Email = "other@test.com", PasswordHash = "h", Role = UserRole.User };
        db.Users.AddRange(owner, other);

        var contents = new Content[contentCount];
        for (var i = 0; i < contentCount; i++)
        {
            contents[i] = new Content
            {
                Title = $"Movie {i}",
                Slug = $"movie-{i}",
                Description = "d",
                Type = ContentType.Movie,
                Status = ContentStatus.Published,
            };
            db.Contents.Add(contents[i]);
        }
        db.SaveChanges();

        return (new PlaylistService(db), db, owner, other, contents);
    }

    [Fact]
    public async Task CreatePlaylist_Success()
    {
        var (service, db, owner, _, _) = BuildSubject();

        var result = await service.CreateAsync(owner.Id, new CreatePlaylistRequest("Favs", "my faves", IsPublic: false, CoverImageUrl: null));

        result.Success.Should().BeTrue();
        result.Data!.Name.Should().Be("Favs");
        result.Data.ItemCount.Should().Be(0);

        var row = await db.Playlists.AsNoTracking().SingleAsync();
        row.UserId.Should().Be(owner.Id);
        row.IsPublic.Should().BeFalse();
    }

    [Fact]
    public async Task OwnerCanUpdate_NonOwnerCannot()
    {
        var (service, _, owner, other, _) = BuildSubject();
        var created = await service.CreateAsync(owner.Id, new CreatePlaylistRequest("Original", null, false, null));
        var playlistId = created.Data!.Id;

        var byOwner = await service.UpdateAsync(owner.Id, playlistId, new UpdatePlaylistRequest("Renamed", null, false, null));
        byOwner.Success.Should().BeTrue();
        byOwner.Data!.Name.Should().Be("Renamed");

        var byStranger = await service.UpdateAsync(other.Id, playlistId, new UpdatePlaylistRequest("Hijacked", null, false, null));
        byStranger.Success.Should().BeFalse();
        byStranger.Error.Should().Contain("Only the owner");
    }

    [Fact]
    public async Task AddDuplicateItem_Fails()
    {
        var (service, _, owner, _, contents) = BuildSubject();
        var created = await service.CreateAsync(owner.Id, new CreatePlaylistRequest("Favs", null, false, null));
        var playlistId = created.Data!.Id;

        var first = await service.AddItemAsync(owner.Id, playlistId, new AddItemToPlaylistRequest(contents[0].Id, null));
        first.Success.Should().BeTrue();

        var second = await service.AddItemAsync(owner.Id, playlistId, new AddItemToPlaylistRequest(contents[0].Id, null));
        second.Success.Should().BeFalse();
        second.Error.Should().Contain("already");
    }

    [Fact]
    public async Task ReorderItems_UpdatesIndices()
    {
        var (service, db, owner, _, contents) = BuildSubject();
        var created = await service.CreateAsync(owner.Id, new CreatePlaylistRequest("Favs", null, false, null));
        var playlistId = created.Data!.Id;

        var i1 = (await service.AddItemAsync(owner.Id, playlistId, new AddItemToPlaylistRequest(contents[0].Id, null))).Data!.Id;
        var i2 = (await service.AddItemAsync(owner.Id, playlistId, new AddItemToPlaylistRequest(contents[1].Id, null))).Data!.Id;
        var i3 = (await service.AddItemAsync(owner.Id, playlistId, new AddItemToPlaylistRequest(contents[2].Id, null))).Data!.Id;

        var result = await service.ReorderItemsAsync(owner.Id, playlistId,
            new ReorderPlaylistRequest(new List<Guid> { i3, i1, i2 }));

        result.Success.Should().BeTrue();

        var rows = await db.PlaylistItems.AsNoTracking()
            .Where(i => i.PlaylistId == playlistId && !i.IsDeleted)
            .OrderBy(i => i.OrderIndex)
            .ToListAsync();
        rows.Select(r => r.Id).Should().Equal(i3, i1, i2);
        rows.Select(r => r.OrderIndex).Should().Equal(0, 1, 2);
    }

    [Fact]
    public async Task PublicPlaylist_VisibleToOthers()
    {
        var (service, _, owner, other, _) = BuildSubject();

        var privatePlaylist = (await service.CreateAsync(owner.Id, new CreatePlaylistRequest("Private", null, IsPublic: false, null))).Data!;
        var publicPlaylist = (await service.CreateAsync(owner.Id, new CreatePlaylistRequest("Public", null, IsPublic: true, null))).Data!;

        var publicList = await service.GetPublicAsync(1, 10, search: null);
        publicList.TotalCount.Should().Be(1);
        publicList.Items.Should().ContainSingle(p => p.Id == publicPlaylist.Id);

        var asStrangerPrivate = await service.GetByIdAsync(privatePlaylist.Id, other.Id);
        asStrangerPrivate.Success.Should().BeFalse();
        asStrangerPrivate.Error.Should().Contain("private");

        var asStrangerPublic = await service.GetByIdAsync(publicPlaylist.Id, other.Id);
        asStrangerPublic.Success.Should().BeTrue();
        asStrangerPublic.Data!.Name.Should().Be("Public");

        var anonymousPublic = await service.GetByIdAsync(publicPlaylist.Id, currentUserId: null);
        anonymousPublic.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeletePlaylist_CascadesToItems()
    {
        var (service, db, owner, _, contents) = BuildSubject();
        var created = await service.CreateAsync(owner.Id, new CreatePlaylistRequest("Favs", null, false, null));
        var playlistId = created.Data!.Id;
        foreach (var c in contents)
            await service.AddItemAsync(owner.Id, playlistId, new AddItemToPlaylistRequest(c.Id, null));

        var activeItems = await db.PlaylistItems.AsNoTracking().CountAsync(i => i.PlaylistId == playlistId && !i.IsDeleted);
        activeItems.Should().Be(contents.Length);

        var result = await service.DeleteAsync(owner.Id, playlistId);

        result.Success.Should().BeTrue();
        var playlist = await db.Playlists.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == playlistId);
        playlist.IsDeleted.Should().BeTrue();

        var aliveAfter = await db.PlaylistItems.AsNoTracking().CountAsync(i => i.PlaylistId == playlistId && !i.IsDeleted);
        aliveAfter.Should().Be(0);

        var allItemsIncludingDeleted = await db.PlaylistItems.IgnoreQueryFilters().AsNoTracking().CountAsync(i => i.PlaylistId == playlistId);
        allItemsIncludingDeleted.Should().Be(contents.Length);
    }
}
