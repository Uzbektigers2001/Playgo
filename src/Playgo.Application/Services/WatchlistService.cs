using Microsoft.EntityFrameworkCore;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.UserActivity;
using Playgo.Domain.Entities;

namespace Playgo.Application.Services;

public class WatchlistService : IWatchlistService
{
    private readonly IApplicationDbContext _db;

    public WatchlistService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<WatchlistItemDto>> GetUserWatchlistAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var query = _db.Watchlists
            .AsNoTracking()
            .Include(w => w.Content)
            .Where(w => !w.IsDeleted && w.UserId == userId);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(w => w.Priority ?? int.MaxValue)
            .ThenByDescending(w => w.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new WatchlistItemDto(
                w.Id,
                w.ContentId,
                w.Content.Title,
                w.Content.PosterUrl,
                w.Priority,
                w.Note,
                w.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<WatchlistItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<Result> AddAsync(Guid userId, AddToWatchlistRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ContentId == Guid.Empty)
            return Result.Fail("contentId is required.");

        var contentExists = await _db.Contents.AnyAsync(c => c.Id == request.ContentId && !c.IsDeleted, cancellationToken);
        if (!contentExists)
            return Result.Fail("Content not found.");

        var alreadyActive = await _db.Watchlists
            .AnyAsync(w => w.UserId == userId && w.ContentId == request.ContentId && !w.IsDeleted, cancellationToken);
        if (alreadyActive)
            return Result.Fail("Content is already in your watchlist.");

        // Revive a previously soft-deleted row if one exists — keeps the partial unique index happy.
        var dormant = await _db.Watchlists
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ContentId == request.ContentId && w.IsDeleted, cancellationToken);
        if (dormant is not null)
        {
            dormant.IsDeleted = false;
            dormant.Priority = request.Priority;
            dormant.Note = request.Note;
            dormant.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Watchlists.Add(new Watchlist
            {
                UserId = userId,
                ContentId = request.ContentId,
                Priority = request.Priority,
                Note = request.Note,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> RemoveAsync(Guid userId, Guid contentId, CancellationToken cancellationToken = default)
    {
        var entry = await _db.Watchlists
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ContentId == contentId && !w.IsDeleted, cancellationToken);
        if (entry is null)
            return Result.Fail("Not in watchlist.");

        entry.IsDeleted = true;
        entry.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<bool> IsInWatchlistAsync(Guid userId, Guid contentId, CancellationToken cancellationToken = default)
    {
        return await _db.Watchlists
            .AnyAsync(w => w.UserId == userId && w.ContentId == contentId && !w.IsDeleted, cancellationToken);
    }

    public async Task<Result> UpdatePriorityAsync(Guid userId, Guid contentId, UpdateWatchlistPriorityRequest request, CancellationToken cancellationToken = default)
    {
        var entry = await _db.Watchlists
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ContentId == contentId && !w.IsDeleted, cancellationToken);
        if (entry is null)
            return Result.Fail("Not in watchlist.");

        entry.Priority = request.Priority;
        entry.Note = request.Note ?? entry.Note;
        entry.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
