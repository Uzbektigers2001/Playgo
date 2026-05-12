namespace Playgo.Application.DTOs.Content;

public record MovieDto(
    Guid Id,
    string Title,
    string? TitleUz,
    string? TitleRu,
    string Description,
    string? DescriptionUz,
    string? DescriptionRu,
    string Slug,
    string? Poster,
    string? Backdrop,
    string? Trailer,
    string? StreamUrl,
    string? HlsManifestUrl,
    int? Duration,
    int? ReleaseYear,
    double Rating,
    List<string> Genres,
    string? Director,
    List<string> Cast,
    string? Country,
    string? Language,
    string Quality,
    long Views,
    int Likes,
    int Dislikes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record LegacyPagedResponse<T>(
    IEnumerable<T> Data,
    int Total,
    int Page,
    int TotalPages);