namespace Playgo.Application.DTOs.UserActivity;

public record WatchlistItemDto(
    Guid Id,
    Guid ContentId,
    string ContentTitle,
    string? PosterUrl,
    int? Priority,
    string? Note,
    DateTime AddedAt);

public record AddToWatchlistRequest(
    Guid ContentId,
    int? Priority,
    string? Note);

public record UpdateWatchlistPriorityRequest(
    int? Priority,
    string? Note);
