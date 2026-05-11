using Microsoft.EntityFrameworkCore;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Content;
using Playgo.Domain.Entities;

namespace Playgo.Application.Services;

public class FavoriteService : IFavoriteService
{
    private readonly IApplicationDbContext _db;

    public FavoriteService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<FavoriteDto>> GetUserFavoritesAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var query = _db.Favorites
            .AsNoTracking()
            .Include(f => f.Content)
            .Where(f => !f.IsDeleted && f.UserId == userId);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FavoriteDto(
                f.Id,
                f.ContentId,
                f.Content.Title,
                f.Content.PosterUrl,
                f.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<FavoriteDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<Result> AddAsync(Guid userId, Guid contentId, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Favorites.AnyAsync(
            f => f.UserId == userId && f.ContentId == contentId,
            cancellationToken);

        if (exists)
            return Result.Ok();

        var contentExists = await _db.Contents.AnyAsync(c => c.Id == contentId && !c.IsDeleted, cancellationToken);
        if (!contentExists)
            return Result.Fail("Content not found.");

        _db.Favorites.Add(new Favorite { UserId = userId, ContentId = contentId });
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> RemoveAsync(Guid userId, Guid contentId, CancellationToken cancellationToken = default)
    {
        var favorite = await _db.Favorites.FirstOrDefaultAsync(
            f => f.UserId == userId && f.ContentId == contentId && !f.IsDeleted,
            cancellationToken);

        if (favorite is null)
            return Result.Ok();

        favorite.IsDeleted = true;
        favorite.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<bool> IsFavoriteAsync(Guid userId, Guid contentId, CancellationToken cancellationToken = default)
    {
        return await _db.Favorites.AnyAsync(
            f => f.UserId == userId && f.ContentId == contentId && !f.IsDeleted,
            cancellationToken);
    }
}
