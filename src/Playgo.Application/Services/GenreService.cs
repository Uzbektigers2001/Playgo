using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Content;
using Playgo.Domain.Entities;

namespace Playgo.Application.Services;

public class GenreService : IGenreService
{
    private readonly IApplicationDbContext _db;

    public GenreService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<GenreDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Genres
            .AsNoTracking()
            .Where(g => !g.IsDeleted)
            .OrderBy(g => g.Name)
            .Select(g => new GenreDto(g.Id, g.Name, g.Slug, g.IconUrl))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<GenreDto>> CreateAsync(CreateGenreRequest request, CancellationToken cancellationToken = default)
    {
        var slug = GenerateSlug(request.Name);
        if (string.IsNullOrEmpty(slug))
            return Result<GenreDto>.Fail("Invalid genre name.");

        if (await _db.Genres.AnyAsync(g => g.Slug == slug && !g.IsDeleted, cancellationToken))
            return Result<GenreDto>.Fail("A genre with that name already exists.");

        var genre = new Genre
        {
            Name = request.Name,
            Slug = slug,
            Description = request.Description,
            IconUrl = request.IconUrl,
        };

        _db.Genres.Add(genre);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<GenreDto>.Ok(new GenreDto(genre.Id, genre.Name, genre.Slug, genre.IconUrl));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var genre = await _db.Genres.FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted, cancellationToken);
        if (genre is null)
            return Result.Fail("Genre not found.");

        genre.IsDeleted = true;
        genre.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<GenreTranslationDto>> UpsertTranslationAsync(Guid genreId, UpsertGenreTranslationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.LanguageCode))
            return Result<GenreTranslationDto>.Fail("languageCode is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<GenreTranslationDto>.Fail("name is required.");

        var genreExists = await _db.Genres.AnyAsync(g => g.Id == genreId && !g.IsDeleted, cancellationToken);
        if (!genreExists)
            return Result<GenreTranslationDto>.Fail("Genre not found.");

        var lang = NormalizeLangOrDefault(request.LanguageCode);

        var existing = await _db.GenreTranslations
            .FirstOrDefaultAsync(t => t.GenreId == genreId && t.LanguageCode == lang, cancellationToken);

        if (existing is null)
        {
            existing = new GenreTranslation
            {
                GenreId = genreId,
                LanguageCode = lang,
                Name = request.Name,
                Description = request.Description,
            };
            _db.GenreTranslations.Add(existing);
        }
        else
        {
            existing.Name = request.Name;
            existing.Description = request.Description;
            existing.IsDeleted = false;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<GenreTranslationDto>.Ok(new GenreTranslationDto(existing.LanguageCode, existing.Name, existing.Description));
    }

    public async Task<Result> DeleteTranslationAsync(Guid genreId, string languageCode, CancellationToken cancellationToken = default)
    {
        var lang = NormalizeLangOrDefault(languageCode);
        var existing = await _db.GenreTranslations
            .FirstOrDefaultAsync(t => t.GenreId == genreId && t.LanguageCode == lang && !t.IsDeleted, cancellationToken);

        if (existing is null)
            return Result.Fail("Translation not found.");

        existing.IsDeleted = true;
        existing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<List<GenreTranslationDto>> GetTranslationsAsync(Guid genreId, CancellationToken cancellationToken = default)
    {
        return await _db.GenreTranslations
            .AsNoTracking()
            .Where(t => t.GenreId == genreId && !t.IsDeleted)
            .OrderBy(t => t.LanguageCode)
            .Select(t => new GenreTranslationDto(t.LanguageCode, t.Name, t.Description))
            .ToListAsync(cancellationToken);
    }

    private static string NormalizeLangOrDefault(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) return "en";
        var lower = lang.Trim().ToLowerInvariant();
        return lower is "uz" or "ru" or "en" ? lower : "en";
    }

    private static string GenerateSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var lower = input.Trim().ToLowerInvariant();
        var cleaned = Regex.Replace(lower, "[^a-z0-9]+", "-");
        return cleaned.Trim('-');
    }
}
