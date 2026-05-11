namespace Playgo.Application.DTOs.UserActivity;

public record PlaylistDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsPublic,
    string? CoverImageUrl,
    int ItemCount,
    DateTime CreatedAt);

public record PlaylistDetailDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsPublic,
    string? CoverImageUrl,
    DateTime CreatedAt,
    List<PlaylistItemDto> Items);

public record PlaylistItemDto(
    Guid Id,
    Guid ContentId,
    string ContentTitle,
    string? PosterUrl,
    int OrderIndex);

public record CreatePlaylistRequest(
    string Name,
    string? Description,
    bool IsPublic,
    string? CoverImageUrl);

public record UpdatePlaylistRequest(
    string Name,
    string? Description,
    bool IsPublic,
    string? CoverImageUrl);

public record AddItemToPlaylistRequest(
    Guid ContentId,
    int? OrderIndex);

public record ReorderPlaylistRequest(
    List<Guid> ItemIdsInOrder);
