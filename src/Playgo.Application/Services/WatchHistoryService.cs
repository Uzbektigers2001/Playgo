using Microsoft.EntityFrameworkCore;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Content;
using Playgo.Domain.Entities;

namespace Playgo.Application.Services;

public class WatchHistoryService : IWatchHistoryService
{
    private readonly IApplicationDbContext _db;

    public WatchHistoryService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<WatchHistoryDto>> GetUserHistoryAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var query = _db.WatchHistories
            .AsNoTracking()
            .Include(w => w.Content)
            .Include(w => w.Episode)
            .Where(w => !w.IsDeleted && w.UserId == userId);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(w => w.LastWatchedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new WatchHistoryDto(
                w.Id,
                w.ContentId,
                w.Content.Title,
                w.Content.PosterUrl,
                w.EpisodeId,
                w.Episode != null ? w.Episode.Title : null,
                w.PositionSeconds,
                w.DurationSeconds,
                w.DurationSeconds > 0 ? (double)w.PositionSeconds / w.DurationSeconds * 100 : 0,
                w.IsCompleted,
                w.LastWatchedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<WatchHistoryDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<List<WatchHistoryDto>> GetContinueWatchingAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
    {
        return await _db.WatchHistories
            .AsNoTracking()
            .Include(w => w.Content)
            .Include(w => w.Episode)
            .Where(w => !w.IsDeleted
                && w.UserId == userId
                && !w.IsCompleted
                && w.PositionSeconds > 10)
            .OrderByDescending(w => w.LastWatchedAt)
            .Take(limit)
            .Select(w => new WatchHistoryDto(
                w.Id,
                w.ContentId,
                w.Content.Title,
                w.Content.PosterUrl,
                w.EpisodeId,
                w.Episode != null ? w.Episode.Title : null,
                w.PositionSeconds,
                w.DurationSeconds,
                w.DurationSeconds > 0 ? (double)w.PositionSeconds / w.DurationSeconds * 100 : 0,
                w.IsCompleted,
                w.LastWatchedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result> UpdateProgressAsync(Guid userId, UpdateWatchProgressRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _db.WatchHistories.FirstOrDefaultAsync(
            w => w.UserId == userId
                && w.ContentId == request.ContentId
                && w.EpisodeId == request.EpisodeId,
            cancellationToken);

        var isCompleted = request.DurationSeconds > 0
            && request.PositionSeconds >= request.DurationSeconds * 0.9;

        if (existing is null)
        {
            _db.WatchHistories.Add(new WatchHistory
            {
                UserId = userId,
                ContentId = request.ContentId,
                EpisodeId = request.EpisodeId,
                PositionSeconds = request.PositionSeconds,
                DurationSeconds = request.DurationSeconds,
                IsCompleted = isCompleted,
                LastWatchedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.PositionSeconds = request.PositionSeconds;
            existing.DurationSeconds = request.DurationSeconds;
            existing.IsCompleted = isCompleted;
            existing.LastWatchedAt = DateTime.UtcNow;
            existing.IsDeleted = false;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> ClearHistoryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _db.WatchHistories
            .Where(w => w.UserId == userId && !w.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var w in items)
        {
            w.IsDeleted = true;
            w.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
