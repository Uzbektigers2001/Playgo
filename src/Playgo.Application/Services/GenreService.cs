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

    private static string GenerateSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var lower = input.Trim().ToLowerInvariant();
        var cleaned = Regex.Replace(lower, "[^a-z0-9]+", "-");
        return cleaned.Trim('-');
    }
}
