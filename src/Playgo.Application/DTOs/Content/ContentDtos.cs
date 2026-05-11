using Playgo.Domain.Enums;

namespace Playgo.Application.DTOs.Content;

public record GenreDto(
    Guid Id,
    string Name,
    string Slug,
    string? IconUrl);

public record EpisodeDto(
    Guid Id,
    int EpisodeNumber,
    string Title,
    string? Description,
    int? DurationMinutes,
    string? ThumbnailUrl,
    string VideoUrl,
    string? HlsManifestUrl,
    DateTime? ReleaseDate);

public record SeasonDto(
    Guid Id,
    int SeasonNumber,
    string? Title,
    string? Description,
    string? PosterUrl,
    DateTime? ReleaseDate,
    List<EpisodeDto> Episodes);

public record ContentListItemDto(
    Guid Id,
    string Title,
    string Slug,
    string? ShortDescription,
    ContentType Type,
    int? ReleaseYear,
    string? PosterUrl,
    string? BackdropUrl,
    double AverageRating,
    long ViewCount,
    List<string> Genres);

public record ContentDetailDto(
    Guid Id,
    string Title,
    string? OriginalTitle,
    string Slug,
    string Description,
    ContentType Type,
    ContentStatus Status,
    int? ReleaseYear,
    DateTime? ReleaseDate,
    int? DurationMinutes,
    string? Country,
    string? Language,
    string? AgeRating,
    string? PosterUrl,
    string? BackdropUrl,
    string? TrailerUrl,
    string? VideoUrl,
    string? HlsManifestUrl,
    string? Director,
    string? Cast,
    double AverageRating,
    int RatingCount,
    long ViewCount,
    bool IsFeatured,
    List<GenreDto> Genres,
    List<SeasonDto> Seasons);

public record CreateContentRequest(
    string Title,
    string? OriginalTitle,
    string Description,
    string? ShortDescription,
    ContentType Type,
    int? ReleaseYear,
    DateTime? ReleaseDate,
    int? DurationMinutes,
    string? Country,
    string? Language,
    string? AgeRating,
    string? PosterUrl,
    string? BackdropUrl,
    string? TrailerUrl,
    string? VideoUrl,
    string? HlsManifestUrl,
    string? Director,
    string? Cast,
    bool IsFeatured,
    List<Guid> GenreIds);

public record UpdateContentRequest(
    string Title,
    string? OriginalTitle,
    string Description,
    string? ShortDescription,
    ContentType Type,
    int? ReleaseYear,
    DateTime? ReleaseDate,
    int? DurationMinutes,
    string? Country,
    string? Language,
    string? AgeRating,
    string? PosterUrl,
    string? BackdropUrl,
    string? TrailerUrl,
    string? VideoUrl,
    string? HlsManifestUrl,
    string? Director,
    string? Cast,
    bool IsFeatured,
    List<Guid> GenreIds,
    ContentStatus Status,
    bool IsTrending);

public record CreateSeasonRequest(
    Guid ContentId,
    int SeasonNumber,
    string? Title,
    string? Description,
    string? PosterUrl,
    DateTime? ReleaseDate);

public record CreateEpisodeRequest(
    Guid SeasonId,
    int EpisodeNumber,
    string Title,
    string? Description,
    int? DurationMinutes,
    string? ThumbnailUrl,
    string VideoUrl,
    string? HlsManifestUrl,
    DateTime? ReleaseDate);

public record CreateGenreRequest(
    string Name,
    string? Description,
    string? IconUrl);

public class ContentFilterRequest
{
    public string? Search { get; set; }
    public ContentType? Type { get; set; }
    public Guid? GenreId { get; set; }
    public int? Year { get; set; }
    public string? Country { get; set; }
    public string? SortBy { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
