using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Content;
using Playgo.Domain.Entities;

namespace Playgo.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IConfiguration _configuration;

    public ReviewService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IConfiguration configuration)
    {
        _db = db;
        _currentUser = currentUser;
        _configuration = configuration;
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

        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.ContentId,
                r.UserId,
                r.User.Username,
                UserAvatar = r.User.AvatarUrl,
                r.Rating,
                r.Comment,
                r.LikesCount,
                r.DislikesCount,
                r.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var myVoteByReview = await GetMyVotesAsync(rows.Select(r => r.Id).ToList(), cancellationToken);

        return new PagedResult<ReviewDto>
        {
            Items = rows.Select(r => new ReviewDto(
                r.Id,
                r.ContentId,
                r.UserId,
                r.Username,
                r.UserAvatar,
                r.Rating,
                r.Comment,
                r.LikesCount,
                r.DislikesCount,
                myVoteByReview.TryGetValue(r.Id, out var v) ? VoteToString(v) : null,
                r.CreatedAt)).ToList(),
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

        var requireModeration = bool.TryParse(_configuration["Reviews:RequireModeration"], out var flag) && flag;

        var review = new Review
        {
            UserId = userId,
            ContentId = request.ContentId,
            Rating = request.Rating,
            Comment = request.Comment,
            IsApproved = !requireModeration,
        };

        _db.Reviews.Add(review);

        if (review.IsApproved)
        {
            var oldSum = content.AverageRating * content.RatingCount;
            var newCount = content.RatingCount + 1;
            content.AverageRating = (oldSum + request.Rating) / newCount;
            content.RatingCount = newCount;
            content.UpdatedAt = DateTime.UtcNow;
        }

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
            review.DislikesCount,
            null,
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

        if (review.IsApproved && content.RatingCount > 0)
        {
            var oldSum = content.AverageRating * content.RatingCount;
            content.AverageRating = (oldSum - review.Rating + request.Rating) / content.RatingCount;
        }

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
            review.DislikesCount,
            null,
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
        if (content is not null && review.IsApproved)
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

    public async Task<Result<ReviewVoteResultDto>> ToggleVoteAsync(Guid userId, Guid reviewId, ReviewVoteType voteType, CancellationToken cancellationToken = default)
    {
        var review = await _db.Reviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && !r.IsDeleted, cancellationToken);
        if (review is null)
            return Result<ReviewVoteResultDto>.Fail("Review not found.");

        var existing = await _db.ReviewVotes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(rv => rv.ReviewId == reviewId && rv.UserId == userId, cancellationToken);

        string? myVote;

        if (existing is null)
        {
            // First-time vote — create row and bump the matching counter.
            _db.ReviewVotes.Add(new ReviewVote
            {
                ReviewId = reviewId,
                UserId = userId,
                VoteType = voteType,
            });
            ApplyDelta(review, voteType, +1);
            myVote = VoteToString(voteType);
        }
        else if (!existing.IsDeleted && existing.VoteType == voteType)
        {
            // Same vote → toggle OFF.
            existing.IsDeleted = true;
            existing.UpdatedAt = DateTime.UtcNow;
            ApplyDelta(review, voteType, -1);
            myVote = null;
        }
        else if (existing.IsDeleted)
        {
            // Revive a dormant row with the new vote.
            existing.IsDeleted = false;
            existing.VoteType = voteType;
            existing.UpdatedAt = DateTime.UtcNow;
            ApplyDelta(review, voteType, +1);
            myVote = VoteToString(voteType);
        }
        else
        {
            // Switch like ↔ dislike.
            var previous = existing.VoteType;
            existing.VoteType = voteType;
            existing.UpdatedAt = DateTime.UtcNow;
            ApplyDelta(review, previous, -1);
            ApplyDelta(review, voteType, +1);
            myVote = VoteToString(voteType);
        }

        review.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Result<ReviewVoteResultDto>.Ok(new ReviewVoteResultDto(
            review.LikesCount,
            review.DislikesCount,
            myVote));
    }

    private static void ApplyDelta(Review review, ReviewVoteType voteType, int delta)
    {
        if (voteType == ReviewVoteType.Like)
            review.LikesCount = Math.Max(0, review.LikesCount + delta);
        else
            review.DislikesCount = Math.Max(0, review.DislikesCount + delta);
    }

    private static string VoteToString(ReviewVoteType voteType) =>
        voteType == ReviewVoteType.Like ? "like" : "dislike";

    private async Task<Dictionary<Guid, ReviewVoteType>> GetMyVotesAsync(List<Guid> reviewIds, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId;
        if (currentUserId is null || reviewIds.Count == 0)
            return new Dictionary<Guid, ReviewVoteType>();

        var rows = await _db.ReviewVotes
            .AsNoTracking()
            .Where(rv => !rv.IsDeleted && rv.UserId == currentUserId.Value && reviewIds.Contains(rv.ReviewId))
            .Select(rv => new { rv.ReviewId, rv.VoteType })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.ReviewId, r => r.VoteType);
    }
}
