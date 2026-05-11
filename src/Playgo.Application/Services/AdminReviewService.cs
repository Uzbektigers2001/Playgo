using Microsoft.EntityFrameworkCore;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Content;

namespace Playgo.Application.Services;

public class AdminReviewService : IAdminReviewService
{
    private readonly IApplicationDbContext _db;

    public AdminReviewService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<ReviewDto>> GetAllAsync(AdminReviewFilter filter, CancellationToken cancellationToken = default)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

        var query = _db.Reviews
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => !r.IsDeleted);

        if (filter.ContentId.HasValue)
            query = query.Where(r => r.ContentId == filter.ContentId.Value);
        if (filter.IsApproved.HasValue)
            query = query.Where(r => r.IsApproved == filter.IsApproved.Value);
        if (filter.UserId.HasValue)
            query = query.Where(r => r.UserId == filter.UserId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(r =>
                (r.Comment != null && r.Comment.ToLower().Contains(s)) ||
                r.User.Username.ToLower().Contains(s));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReviewDto(
                r.Id,
                r.ContentId,
                r.UserId,
                r.User.Username,
                r.User.AvatarUrl,
                r.Rating,
                r.Comment,
                r.LikesCount,
                r.DislikesCount,
                null,
                r.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<ReviewDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<Result> ApproveAsync(Guid reviewId, CancellationToken cancellationToken = default)
    {
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId && !r.IsDeleted, cancellationToken);
        if (review is null) return Result.Fail("Review not found.");

        if (!review.IsApproved)
        {
            review.IsApproved = true;
            review.RejectionReason = null;

            // Re-introduce the rating into the content average (it was excluded while pending).
            var content = await _db.Contents.FirstOrDefaultAsync(c => c.Id == review.ContentId, cancellationToken);
            if (content is not null)
            {
                var oldSum = content.AverageRating * content.RatingCount;
                var newCount = content.RatingCount + 1;
                content.AverageRating = (oldSum + review.Rating) / newCount;
                content.RatingCount = newCount;
                content.UpdatedAt = DateTime.UtcNow;
            }
        }

        review.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> RejectAsync(Guid reviewId, string? reason, CancellationToken cancellationToken = default)
    {
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId && !r.IsDeleted, cancellationToken);
        if (review is null) return Result.Fail("Review not found.");

        if (review.IsApproved)
        {
            // Strip the rating from the aggregate when un-approving an approved review.
            var content = await _db.Contents.FirstOrDefaultAsync(c => c.Id == review.ContentId, cancellationToken);
            if (content is not null && content.RatingCount > 0)
            {
                var oldSum = content.AverageRating * content.RatingCount;
                var newCount = content.RatingCount - 1;
                if (newCount <= 0)
                {
                    content.AverageRating = 0;
                    content.RatingCount = 0;
                }
                else
                {
                    content.AverageRating = (oldSum - review.Rating) / newCount;
                    content.RatingCount = newCount;
                }
                content.UpdatedAt = DateTime.UtcNow;
            }
        }

        review.IsApproved = false;
        review.RejectionReason = reason;
        review.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(Guid reviewId, CancellationToken cancellationToken = default)
    {
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId && !r.IsDeleted, cancellationToken);
        if (review is null) return Result.Fail("Review not found.");

        if (review.IsApproved)
        {
            var content = await _db.Contents.FirstOrDefaultAsync(c => c.Id == review.ContentId, cancellationToken);
            if (content is not null && content.RatingCount > 0)
            {
                var oldSum = content.AverageRating * content.RatingCount;
                var newCount = content.RatingCount - 1;
                if (newCount <= 0)
                {
                    content.AverageRating = 0;
                    content.RatingCount = 0;
                }
                else
                {
                    content.AverageRating = (oldSum - review.Rating) / newCount;
                    content.RatingCount = newCount;
                }
                content.UpdatedAt = DateTime.UtcNow;
            }
        }

        review.IsDeleted = true;
        review.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
