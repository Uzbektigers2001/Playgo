using Microsoft.EntityFrameworkCore;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Content;
using Playgo.Domain.Entities;
using Playgo.Domain.Enums;

namespace Playgo.Application.Services;

public class MoviesAliasService : IMoviesAliasService
{
    private readonly IApplicationDbContext _db;

    public MoviesAliasService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<LegacyPagedResponse<MovieDto>> GetMoviesAsync(
        int page,
        int limit,
        string? search,
        string? genre,
        int? year,
        double? rating,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (limit < 1) limit = 20;

        var query = _db.Contents
            .AsNoTracking()
            .Include(c => c.ContentGenres).ThenInclude(cg => cg.Genre)
            .Where(c => !c.IsDeleted && c.Status == ContentStatus.Published);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(c =>
                c.Title.ToLower().Contains(s) ||
                c.Description.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(genre))
        {
            var g = genre.Trim().ToLower();
            query = query.Where(c =>
                c.ContentGenres.Any(cg => cg.Genre.Slug.ToLower() == g || cg.Genre.Name.ToLower() == g));
        }

        if (year.HasValue)
            query = query.Where(c => c.ReleaseYear == year.Value);

        if (rating.HasValue)
            query = query.Where(c => c.AverageRating >= rating.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var totalPages = limit > 0 ? (int)Math.Ceiling((double)total / limit) : 0;

        return new LegacyPagedResponse<MovieDto>(
            items.Select(Map).ToList(),
            total,
            page,
            totalPages);
    }

    public async Task<List<MovieDto>> GetFeaturedAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit < 1) limit = 10;

        var items = await _db.Contents
            .AsNoTracking()
            .Include(c => c.ContentGenres).ThenInclude(cg => cg.Genre)
            .Where(c => !c.IsDeleted && c.IsFeatured && c.Status == ContentStatus.Published)
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return items.Select(Map).ToList();
    }

    public async Task<List<MovieDto>> GetTrendingAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit < 1) limit = 10;

        var items = await _db.Contents
            .AsNoTracking()
            .Include(c => c.ContentGenres).ThenInclude(cg => cg.Genre)
            .Where(c => !c.IsDeleted && c.Status == ContentStatus.Published)
            .OrderByDescending(c => c.ViewCount)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return items.Select(Map).ToList();
    }

    public async Task<MovieDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var content = await _db.Contents
            .AsNoTracking()
            .Include(c => c.ContentGenres).ThenInclude(cg => cg.Genre)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);

        return content is null ? null : Map(content);
    }

    public async Task<List<MovieDto>> GetRelatedAsync(Guid id, int limit, CancellationToken cancellationToken = default)
    {
        if (limit < 1) limit = 10;

        var genreIds = await _db.ContentGenres
            .Where(cg => cg.ContentId == id)
            .Select(cg => cg.GenreId)
            .ToListAsync(cancellationToken);

        if (genreIds.Count == 0)
            return new List<MovieDto>();

        var items = await _db.Contents
            .AsNoTracking()
            .Include(c => c.ContentGenres).ThenInclude(cg => cg.Genre)
            .Where(c => !c.IsDeleted
                && c.Status == ContentStatus.Published
                && c.Id != id
                && c.ContentGenres.Any(cg => genreIds.Contains(cg.GenreId)))
            .OrderByDescending(c => c.AverageRating)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return items.Select(Map).ToList();
    }

    private static MovieDto Map(Content c)
    {
        var castList = string.IsNullOrWhiteSpace(c.Cast)
            ? new List<string>()
            : c.Cast.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var genres = c.ContentGenres
            .Where(cg => cg.Genre is not null)
            .Select(cg => cg.Genre.Name)
            .ToList();

        return new MovieDto(
            c.Id,
            c.Title,
            null,
            null,
            c.Description,
            null,
            null,
            c.Slug,
            c.PosterUrl,
            c.BackdropUrl,
            c.TrailerUrl,
            c.HlsManifestUrl ?? c.VideoUrl,
            c.HlsManifestUrl,
            c.DurationMinutes,
            c.ReleaseYear,
            c.AverageRating,
            genres,
            c.Director,
            castList,
            c.Country,
            c.Language,
            c.Quality.ToString(),
            c.ViewCount,
            0,
            0,
            c.CreatedAt,
            c.UpdatedAt);
    }
}
