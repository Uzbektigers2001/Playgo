using Playgo.Domain.Entities;

namespace Playgo.Application.DTOs.Content;

public record ReviewDto(
    Guid Id,
    Guid ContentId,
    Guid UserId,
    string Username,
    string? UserAvatar,
    int Rating,
    string? Comment,
    int LikesCount,
    int DislikesCount,
    string? MyVote,
    DateTime CreatedAt);

public record ToggleVoteRequest(ReviewVoteType VoteType);

public record ReviewVoteResultDto(
    int LikesCount,
    int DislikesCount,
    string? MyVote);

public record RejectReviewRequest(string? Reason);

public class AdminReviewFilter
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? ContentId { get; set; }
    public bool? IsApproved { get; set; }
    public Guid? UserId { get; set; }
    public string? Search { get; set; }
}

public record CreateReviewRequest(
    Guid ContentId,
    int Rating,
    string? Comment);

public record UpdateReviewRequest(
    int Rating,
    string? Comment);

public record WatchHistoryDto(
    Guid Id,
    Guid ContentId,
    string ContentTitle,
    string? PosterUrl,
    Guid? EpisodeId,
    string? EpisodeTitle,
    int PositionSeconds,
    int DurationSeconds,
    double ProgressPercent,
    bool IsCompleted,
    DateTime LastWatchedAt);

public record UpdateWatchProgressRequest(
    Guid ContentId,
    Guid? EpisodeId,
    int PositionSeconds,
    int DurationSeconds);

public record FavoriteDto(
    Guid Id,
    Guid ContentId,
    string ContentTitle,
    string? PosterUrl,
    DateTime AddedAt);
