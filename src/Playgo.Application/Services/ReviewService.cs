using Microsoft.EntityFrameworkCore;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Content;
using Playgo.Domain.Entities;

namespace Playgo.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IApplicationDbContext _db;

    public ReviewService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<ReviewDto>> GetReviewsForContentAsync(Guid contentId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var query = _db.Reviews
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => !r.IsDeleted && r.ContentId == contentId && r.IsApproved);

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

    public async Task<Result<ReviewDto>> CreateAsync(Guid userId, CreateReviewRequest request, CancellationToken cancellationToken = default)
    {
        var content = await _db.Contents.FirstOrDefaultAsync(c => c.Id == request.ContentId && !c.IsDeleted, cancellationToken);
        if (content is null)
            return Result<ReviewDto>.Fail("Content not found.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);
        if (user is null)
            return Result<ReviewDto>.Fail("User not found.");

        var review = new Review
        {
            UserId = userId,
            ContentId = request.ContentId,
            Rating = request.Rating,
            Comment = request.Comment,
            IsApproved = true,
        };

        _db.Reviews.Add(review);

        var oldSum = content.AverageRating * content.RatingCount;
        var newCount = content.RatingCount + 1;
        content.AverageRating = (oldSum + request.Rating) / newCount;
        content.RatingCount = newCount;
        content.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Result<ReviewDto>.Ok(new ReviewDto(
            review.Id,
            review.ContentId,
            review.UserId,
            user.Username,
            user.AvatarUrl,
            review.Rating,
            review.Comment,
            review.LikesCount,
            review.CreatedAt));
    }

    public async Task<Result<ReviewDto>> UpdateAsync(Guid userId, Guid reviewId, UpdateReviewRequest request, CancellationToken cancellationToken = default)
    {
        var review = await _db.Reviews
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == reviewId && !r.IsDeleted, cancellationToken);

        if (review is null)
            return Result<ReviewDto>.Fail("Review not found.");

        if (review.UserId != userId)
            return Result<ReviewDto>.Fail("You can only edit your own reviews.");

        var content = await _db.Contents.FirstOrDefaultAsync(c => c.Id == review.ContentId, cancellationToken);
        if (content is null)
            return Result<ReviewDto>.Fail("Content not found.");

        var oldRating = review.Rating;
        var oldSum = content.AverageRating * content.RatingCount;
        if (content.RatingCount > 0)
            content.AverageRating = (oldSum - oldRating + request.Rating) / content.RatingCount;

        review.Rating = request.Rating;
        review.Comment = request.Comment;
        review.UpdatedAt = DateTime.UtcNow;
        content.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Result<ReviewDto>.Ok(new ReviewDto(
            review.Id,
            review.ContentId,
            review.UserId,
            review.User.Username,
            review.User.AvatarUrl,
            review.Rating,
            review.Comment,
            review.LikesCount,
            review.CreatedAt));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid reviewId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId && !r.IsDeleted, cancellationToken);
        if (review is null)
            return Result.Fail("Review not found.");

        if (!isAdmin && review.UserId != userId)
            return Result.Fail("You can only delete your own reviews.");

        var content = await _db.Contents.FirstOrDefaultAsync(c => c.Id == review.ContentId, cancellationToken);
        if (content is not null)
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

        review.IsDeleted = true;
        review.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
