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
    DateTime CreatedAt);

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
