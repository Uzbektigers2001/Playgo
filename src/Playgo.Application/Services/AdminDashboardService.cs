using Microsoft.EntityFrameworkCore;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Admin;
using Playgo.Domain.Enums;

namespace Playgo.Application.Services;

public class AdminDashboardService : IAdminDashboardService
{
    private const string CacheKey = "admin:dashboard:stats";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;

    public AdminDashboardService(IApplicationDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<AdminDashboardStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var cached = await _cache.GetAsync<AdminDashboardStatsDto>(CacheKey, ct);
        if (cached is not null)
            return cached;

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var since30 = DateTime.UtcNow.Date.AddDays(-29);

        var totalUsers = await _db.Users.CountAsync(u => !u.IsDeleted, ct);
        var newUsersThisMonth = await _db.Users.CountAsync(u => !u.IsDeleted && u.CreatedAt >= monthStart, ct);
        var totalContents = await _db.Contents.CountAsync(c => !c.IsDeleted, ct);
        var publishedContents = await _db.Contents.CountAsync(c => !c.IsDeleted && c.Status == ContentStatus.Published, ct);
        var draftContents = await _db.Contents.CountAsync(c => !c.IsDeleted && c.Status == ContentStatus.Draft, ct);
        var totalViews = await _db.Contents.Where(c => !c.IsDeleted).SumAsync(c => (long?)c.ViewCount, ct) ?? 0;
        var totalReviews = await _db.Reviews.CountAsync(r => !r.IsDeleted, ct);

        var rated = _db.Contents.Where(c => !c.IsDeleted && c.RatingCount > 0);
        var averageRating = await rated.AnyAsync(ct)
            ? await rated.AverageAsync(c => c.AverageRating, ct)
            : 0d;

        var totalFavorites = await _db.Favorites.CountAsync(f => !f.IsDeleted, ct);
        var totalWatchlists = await _db.Watchlists.CountAsync(w => !w.IsDeleted, ct);

        var recentMovies = await _db.Contents.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .Take(5)
            .Select(c => new AdminRecentItemDto(c.Id, c.Title, c.Type.ToString(), c.CreatedAt))
            .ToListAsync(ct);

        var recentUsers = await _db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .Select(u => new AdminRecentItemDto(u.Id, u.Username, u.Email, u.CreatedAt))
            .ToListAsync(ct);

        var genreRows = await _db.ContentGenres.AsNoTracking()
            .Where(cg => !cg.Content.IsDeleted)
            .Select(cg => new
            {
                cg.GenreId,
                GenreName = cg.Genre.Name,
                cg.Content.ViewCount,
            })
            .ToListAsync(ct);

        var topGenres = genreRows
            .GroupBy(r => new { r.GenreId, r.GenreName })
            .Select(g => new AdminGenreStatDto(
                g.Key.GenreId,
                g.Key.GenreName,
                g.Count(),
                g.Sum(x => (long)x.ViewCount)))
            .OrderByDescending(g => g.ContentCount)
            .Take(5)
            .ToList();

        var viewsRaw = await _db.ContentViewLogs.AsNoTracking()
            .Where(l => !l.IsDeleted && l.ViewedAt >= since30)
            .Select(l => l.ViewedAt)
            .ToListAsync(ct);

        var byDay = viewsRaw
            .GroupBy(d => d.Date)
            .ToDictionary(g => g.Key, g => (long)g.Count());

        var viewsLast30Days = new List<AdminViewsByDayDto>(30);
        for (int i = 0; i < 30; i++)
        {
            var day = since30.AddDays(i).Date;
            byDay.TryGetValue(day, out var count);
            viewsLast30Days.Add(new AdminViewsByDayDto(day.ToString("yyyy-MM-dd"), count));
        }

        var stats = new AdminDashboardStatsDto(
            totalUsers,
            newUsersThisMonth,
            totalContents,
            publishedContents,
            draftContents,
            totalViews,
            totalReviews,
            averageRating,
            totalFavorites,
            totalWatchlists,
            recentMovies,
            recentUsers,
            topGenres,
            viewsLast30Days);

        await _cache.SetAsync(CacheKey, stats, CacheTtl, ct);
        return stats;
    }
}
