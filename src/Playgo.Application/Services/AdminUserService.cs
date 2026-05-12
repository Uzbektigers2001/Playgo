using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Admin;

namespace Playgo.Application.Services;

public class AdminUserService : IAdminUserService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(IApplicationDbContext db, ILogger<AdminUserService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(AdminUserFilterRequest filter, CancellationToken ct = default)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

        var query = _db.Users.AsNoTracking().Where(u => !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(u =>
                u.Username.ToLower().Contains(s) ||
                u.Email.ToLower().Contains(s) ||
                (u.FullName != null && u.FullName.ToLower().Contains(s)));
        }

        if (filter.Role.HasValue)
            query = query.Where(u => u.Role == filter.Role.Value);

        if (filter.IsBanned.HasValue)
            query = query.Where(u => u.IsBanned == filter.IsBanned.Value);

        if (filter.IsEmailVerified.HasValue)
            query = query.Where(u => u.IsEmailVerified == filter.IsEmailVerified.Value);

        query = filter.SortBy?.ToLowerInvariant() switch
        {
            "lastloginat" => query.OrderByDescending(u => u.LastLoginAt),
            "email" => query.OrderBy(u => u.Email),
            _ => query.OrderByDescending(u => u.CreatedAt),
        };

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserListItemDto(
                u.Id,
                u.Username,
                u.Email,
                u.FullName,
                u.AvatarUrl,
                u.Role,
                u.IsBanned,
                u.IsEmailVerified,
                u.CreatedAt,
                u.LastLoginAt,
                _db.Reviews.Count(r => r.UserId == u.Id && !r.IsDeleted),
                _db.Favorites.Count(f => f.UserId == u.Id && !f.IsDeleted)))
            .ToListAsync(ct);

        return new PagedResult<AdminUserListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<Result<AdminUserDetailDto>> GetUserByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct);
        if (user is null)
            return Result<AdminUserDetailDto>.Fail("User not found.");

        var reviewsCount = await _db.Reviews.CountAsync(r => r.UserId == id && !r.IsDeleted, ct);
        var favoritesCount = await _db.Favorites.CountAsync(f => f.UserId == id && !f.IsDeleted, ct);
        var watchlistCount = await _db.Watchlists.CountAsync(w => w.UserId == id && !w.IsDeleted, ct);
        var playlistsCount = await _db.Playlists.CountAsync(p => p.UserId == id && !p.IsDeleted, ct);

        var recentReviews = await _db.Reviews.AsNoTracking()
            .Where(r => r.UserId == id && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .Select(r => new AdminUserActivityDto("review", r.ContentId, r.Content.Title, r.CreatedAt))
            .ToListAsync(ct);

        var recentFavorites = await _db.Favorites.AsNoTracking()
            .Where(f => f.UserId == id && !f.IsDeleted)
            .OrderByDescending(f => f.CreatedAt)
            .Take(5)
            .Select(f => new AdminUserActivityDto("favorite", f.ContentId, f.Content.Title, f.CreatedAt))
            .ToListAsync(ct);

        var recent = recentReviews.Concat(recentFavorites)
            .OrderByDescending(a => a.OccurredAt)
            .Take(10)
            .ToList();

        return Result<AdminUserDetailDto>.Ok(new AdminUserDetailDto(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.AvatarUrl,
            user.Role,
            user.IsBanned,
            user.IsEmailVerified,
            user.CreatedAt,
            user.LastLoginAt,
            reviewsCount,
            favoritesCount,
            user.BanReason,
            user.BannedAt,
            watchlistCount,
            playlistsCount,
            recent));
    }

    public async Task<Result<AdminUserDetailDto>> UpdateAsync(Guid id, UpdateUserAsAdminRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct);
        if (user is null)
            return Result<AdminUserDetailDto>.Fail("User not found.");

        if (request.FullName is not null)
            user.FullName = request.FullName;

        if (request.AvatarUrl is not null)
            user.AvatarUrl = request.AvatarUrl;

        if (request.Role.HasValue && user.Role != request.Role.Value)
        {
            _logger.LogInformation("Admin changed role for user {UserId} from {OldRole} to {NewRole}",
                user.Id, user.Role, request.Role.Value);
            user.Role = request.Role.Value;
        }

        if (request.IsBanned.HasValue)
        {
            if (request.IsBanned.Value && !user.IsBanned)
            {
                user.IsBanned = true;
                user.BannedAt = DateTime.UtcNow;
                user.BanReason = request.BanReason;
                user.RefreshToken = null;
            }
            else if (!request.IsBanned.Value && user.IsBanned)
            {
                user.IsBanned = false;
                user.BannedAt = null;
                user.BanReason = null;
            }
            else if (request.BanReason is not null && user.IsBanned)
            {
                user.BanReason = request.BanReason;
            }
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetUserByIdAsync(id, ct);
    }

    public async Task<Result> BanAsync(Guid id, string reason, Guid currentAdminId, CancellationToken ct = default)
    {
        if (id == currentAdminId)
            return Result.Fail("Cannot ban yourself.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct);
        if (user is null)
            return Result.Fail("User not found.");

        user.IsBanned = true;
        user.BanReason = reason;
        user.BannedAt = DateTime.UtcNow;
        user.RefreshToken = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Admin {AdminId} banned user {UserId}. Reason: {Reason}", currentAdminId, id, reason);
        return Result.Ok();
    }

    public async Task<Result> UnbanAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct);
        if (user is null)
            return Result.Fail("User not found.");

        user.IsBanned = false;
        user.BanReason = null;
        user.BannedAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(Guid id, Guid currentAdminId, CancellationToken ct = default)
    {
        if (id == currentAdminId)
            return Result.Fail("Cannot delete yourself.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct);
        if (user is null)
            return Result.Fail("User not found.");

        user.IsDeleted = true;
        user.RefreshToken = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Admin {AdminId} deleted user {UserId}", currentAdminId, id);
        return Result.Ok();
    }
}
