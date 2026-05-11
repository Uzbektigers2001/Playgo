namespace Playgo.Application.DTOs.Content;

public record ContentTranslationDto(
    string LanguageCode,
    string Title,
    string? OriginalTitle,
    string Description,
    string? ShortDescription,
    string? Director,
    string? Cast);

public record UpsertContentTranslationRequest(
    string LanguageCode,
    string Title,
    string? OriginalTitle,
    string Description,
    string? ShortDescription,
    string? Director,
    string? Cast);

public record GenreTranslationDto(
    string LanguageCode,
    string Name,
    string? Description);

public record UpsertGenreTranslationRequest(
    string LanguageCode,
    string Name,
    string? Description);
