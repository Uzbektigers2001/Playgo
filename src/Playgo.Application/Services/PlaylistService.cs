using Microsoft.EntityFrameworkCore;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.UserActivity;
using Playgo.Domain.Entities;

namespace Playgo.Application.Services;

public class PlaylistService : IPlaylistService
{
    private readonly IApplicationDbContext _db;

    public PlaylistService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PlaylistDto>> CreateAsync(Guid userId, CreatePlaylistRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<PlaylistDto>.Fail("name is required.");

        var playlist = new Playlist
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Description = request.Description,
            IsPublic = request.IsPublic,
            CoverImageUrl = request.CoverImageUrl,
        };

        _db.Playlists.Add(playlist);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<PlaylistDto>.Ok(new PlaylistDto(
            playlist.Id,
            playlist.Name,
            playlist.Description,
            playlist.IsPublic,
            playlist.CoverImageUrl,
            0,
            playlist.CreatedAt));
    }

    public async Task<Result<PlaylistDto>> UpdateAsync(Guid userId, Guid playlistId, UpdatePlaylistRequest request, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && !p.IsDeleted, cancellationToken);
        if (playlist is null)
            return Result<PlaylistDto>.Fail("Playlist not found.");
        if (playlist.UserId != userId)
            return Result<PlaylistDto>.Fail("Only the owner can update this playlist.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<PlaylistDto>.Fail("name is required.");

        playlist.Name = request.Name.Trim();
        playlist.Description = request.Description;
        playlist.IsPublic = request.IsPublic;
        playlist.CoverImageUrl = request.CoverImageUrl;
        playlist.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var itemCount = await _db.PlaylistItems.CountAsync(i => i.PlaylistId == playlist.Id && !i.IsDeleted, cancellationToken);
        return Result<PlaylistDto>.Ok(new PlaylistDto(
            playlist.Id,
            playlist.Name,
            playlist.Description,
            playlist.IsPublic,
            playlist.CoverImageUrl,
            itemCount,
            playlist.CreatedAt));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid playlistId, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && !p.IsDeleted, cancellationToken);
        if (playlist is null)
            return Result.Fail("Playlist not found.");
        if (playlist.UserId != userId)
            return Result.Fail("Only the owner can delete this playlist.");

        var now = DateTime.UtcNow;
        playlist.IsDeleted = true;
        playlist.UpdatedAt = now;

        var items = await _db.PlaylistItems
            .Where(i => i.PlaylistId == playlistId && !i.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            item.IsDeleted = true;
            item.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<PagedResult<PlaylistDto>> GetMineAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var query = _db.Playlists
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.IsDeleted);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PlaylistDto(
                p.Id,
                p.Name,
                p.Description,
                p.IsPublic,
                p.CoverImageUrl,
                _db.PlaylistItems.Count(i => i.PlaylistId == p.Id && !i.IsDeleted),
                p.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<PlaylistDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<PagedResult<PlaylistDto>> GetPublicAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var query = _db.Playlists
            .AsNoTracking()
            .Where(p => p.IsPublic && !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(s));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PlaylistDto(
                p.Id,
                p.Name,
                p.Description,
                p.IsPublic,
                p.CoverImageUrl,
                _db.PlaylistItems.Count(i => i.PlaylistId == p.Id && !i.IsDeleted),
                p.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<PlaylistDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<Result<PlaylistDetailDto>> GetByIdAsync(Guid playlistId, Guid? currentUserId, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == playlistId && !p.IsDeleted, cancellationToken);
        if (playlist is null)
            return Result<PlaylistDetailDto>.Fail("Playlist not found.");

        if (!playlist.IsPublic && playlist.UserId != currentUserId)
            return Result<PlaylistDetailDto>.Fail("This playlist is private.");

        var items = await _db.PlaylistItems
            .AsNoTracking()
            .Include(i => i.Content)
            .Where(i => i.PlaylistId == playlistId && !i.IsDeleted)
            .OrderBy(i => i.OrderIndex)
            .Select(i => new PlaylistItemDto(
                i.Id,
                i.ContentId,
                i.Content.Title,
                i.Content.PosterUrl,
                i.OrderIndex))
            .ToListAsync(cancellationToken);

        return Result<PlaylistDetailDto>.Ok(new PlaylistDetailDto(
            playlist.Id,
            playlist.Name,
            playlist.Description,
            playlist.IsPublic,
            playlist.CoverImageUrl,
            playlist.CreatedAt,
            items));
    }

    public async Task<Result<PlaylistItemDto>> AddItemAsync(Guid userId, Guid playlistId, AddItemToPlaylistRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ContentId == Guid.Empty)
            return Result<PlaylistItemDto>.Fail("contentId is required.");

        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && !p.IsDeleted, cancellationToken);
        if (playlist is null)
            return Result<PlaylistItemDto>.Fail("Playlist not found.");
        if (playlist.UserId != userId)
            return Result<PlaylistItemDto>.Fail("Only the owner can modify this playlist.");

        var content = await _db.Contents.FirstOrDefaultAsync(c => c.Id == request.ContentId && !c.IsDeleted, cancellationToken);
        if (content is null)
            return Result<PlaylistItemDto>.Fail("Content not found.");

        var duplicate = await _db.PlaylistItems
            .AnyAsync(i => i.PlaylistId == playlistId && i.ContentId == request.ContentId && !i.IsDeleted, cancellationToken);
        if (duplicate)
            return Result<PlaylistItemDto>.Fail("Content already in playlist.");

        int orderIndex;
        if (request.OrderIndex.HasValue && request.OrderIndex.Value >= 0)
        {
            orderIndex = request.OrderIndex.Value;
        }
        else
        {
            var siblings = await _db.PlaylistItems
                .Where(i => i.PlaylistId == playlistId && !i.IsDeleted)
                .Select(i => (int?)i.OrderIndex)
                .ToListAsync(cancellationToken);
            orderIndex = (siblings.Max() ?? -1) + 1;
        }

        var item = new PlaylistItem
        {
            PlaylistId = playlistId,
            ContentId = request.ContentId,
            OrderIndex = orderIndex,
        };
        _db.PlaylistItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<PlaylistItemDto>.Ok(new PlaylistItemDto(
            item.Id,
            item.ContentId,
            content.Title,
            content.PosterUrl,
            item.OrderIndex));
    }

    public async Task<Result> RemoveItemAsync(Guid userId, Guid playlistId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && !p.IsDeleted, cancellationToken);
        if (playlist is null)
            return Result.Fail("Playlist not found.");
        if (playlist.UserId != userId)
            return Result.Fail("Only the owner can modify this playlist.");

        var item = await _db.PlaylistItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.PlaylistId == playlistId && !i.IsDeleted, cancellationToken);
        if (item is null)
            return Result.Fail("Item not found.");

        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> ReorderItemsAsync(Guid userId, Guid playlistId, ReorderPlaylistRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ItemIdsInOrder is null || request.ItemIdsInOrder.Count == 0)
            return Result.Fail("itemIdsInOrder must contain at least one id.");

        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && !p.IsDeleted, cancellationToken);
        if (playlist is null)
            return Result.Fail("Playlist not found.");
        if (playlist.UserId != userId)
            return Result.Fail("Only the owner can reorder this playlist.");

        var items = await _db.PlaylistItems
            .Where(i => i.PlaylistId == playlistId && !i.IsDeleted)
            .ToListAsync(cancellationToken);
        var byId = items.ToDictionary(i => i.Id);

        if (request.ItemIdsInOrder.Distinct().Count() != request.ItemIdsInOrder.Count)
            return Result.Fail("itemIdsInOrder contains duplicate ids.");

        foreach (var id in request.ItemIdsInOrder)
        {
            if (!byId.ContainsKey(id))
                return Result.Fail($"Item {id} does not belong to this playlist.");
        }

        if (request.ItemIdsInOrder.Count != items.Count)
            return Result.Fail("itemIdsInOrder must include every item in the playlist exactly once.");

        var now = DateTime.UtcNow;
        for (var index = 0; index < request.ItemIdsInOrder.Count; index++)
        {
            var item = byId[request.ItemIdsInOrder[index]];
            item.OrderIndex = index;
            item.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
