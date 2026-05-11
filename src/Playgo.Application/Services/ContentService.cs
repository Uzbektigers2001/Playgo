using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Content;
using Playgo.Domain.Entities;
using Playgo.Domain.Enums;

namespace Playgo.Application.Services;

public class ContentService : IContentService
{
    private readonly IApplicationDbContext _db;

    public ContentService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<ContentListItemDto>> GetContentsAsync(ContentFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

        var query = _db.Contents
            .AsNoTracking()
            .Include(c => c.ContentGenres).ThenInclude(cg => cg.Genre)
            .Where(c => !c.IsDeleted && c.Status == ContentStatus.Published);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(c =>
                c.Title.ToLower().Contains(s) ||
                (c.OriginalTitle != null && c.OriginalTitle.ToLower().Contains(s)) ||
                c.Description.ToLower().Contains(s));
        }

        if (filter.Type.HasValue)
            query = query.Where(c => c.Type == filter.Type.Value);

        if (filter.GenreId.HasValue)
            query = query.Where(c => c.ContentGenres.Any(cg => cg.GenreId == filter.GenreId.Value));

        if (filter.Year.HasValue)
            query = query.Where(c => c.ReleaseYear == filter.Year.Value);

        if (!string.IsNullOrWhiteSpace(filter.Country))
            query = query.Where(c => c.Country == filter.Country);

        query = filter.SortBy?.ToLowerInvariant() switch
        {
            "popular" => query.OrderByDescending(c => c.ViewCount),
            "rating" => query.OrderByDescending(c => c.AverageRating),
            "newest" => query.OrderByDescending(c => c.ReleaseDate ?? c.CreatedAt),
            _ => query.OrderByDescending(c => c.CreatedAt),
        };

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ContentListItemDto>
        {
            Items = items.Select(MapToListItem).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<Result<ContentDetailDto>> GetContentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var content = await LoadWithRelationsAsync(c => c.Id == id, cancellationToken);
        if (content is null)
            return Result<ContentDetailDto>.Fail("Content not found.");

        return Result<ContentDetailDto>.Ok(MapToDetail(content));
    }

    public async Task<Result<ContentDetailDto>> GetContentBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var content = await LoadWithRelationsAsync(c => c.Slug == slug, cancellationToken);
        if (content is null)
            return Result<ContentDetailDto>.Fail("Content not found.");

        return Result<ContentDetailDto>.Ok(MapToDetail(content));
    }

    public async Task<List<ContentListItemDto>> GetFeaturedAsync(int limit, CancellationToken cancellationToken = default)
    {
        var items = await _db.Contents
            .AsNoTracking()
            .Include(c => c.ContentGenres).ThenInclude(cg => cg.Genre)
            .Where(c => !c.IsDeleted && c.IsFeatured && c.Status == ContentStatus.Published)
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return items.Select(MapToListItem).ToList();
    }

    public async Task<List<ContentListItemDto>> GetTrendingAsync(int limit, CancellationToken cancellationToken = default)
    {
        var items = await _db.Contents
            .AsNoTracking()
            .Include(c => c.ContentGenres).ThenInclude(cg => cg.Genre)
            .Where(c => !c.IsDeleted && c.Status == ContentStatus.Published)
            .OrderByDescending(c => c.ViewCount)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return items.Select(MapToListItem).ToList();
    }

    public async Task<List<ContentListItemDto>> GetSimilarAsync(Guid contentId, int limit, CancellationToken cancellationToken = default)
    {
        var genreIds = await _db.ContentGenres
            .Where(cg => cg.ContentId == contentId)
            .Select(cg => cg.GenreId)
            .ToListAsync(cancellationToken);

        if (genreIds.Count == 0)
            return new List<ContentListItemDto>();

        var items = await _db.Contents
            .AsNoTracking()
            .Include(c => c.ContentGenres).ThenInclude(cg => cg.Genre)
            .Where(c => !c.IsDeleted
                && c.Status == ContentStatus.Published
                && c.Id != contentId
                && c.ContentGenres.Any(cg => genreIds.Contains(cg.GenreId)))
            .OrderByDescending(c => c.AverageRating)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return items.Select(MapToListItem).ToList();
    }

    public async Task<Result<ContentDetailDto>> CreateAsync(CreateContentRequest request, CancellationToken cancellationToken = default)
    {
        var slug = GenerateSlug(request.Title);
        if (string.IsNullOrEmpty(slug))
            slug = Guid.NewGuid().ToString("N").Substring(0, 8);

        if (await _db.Contents.AnyAsync(c => c.Slug == slug, cancellationToken))
            slug = $"{slug}-{Guid.NewGuid().ToString("N").Substring(0, 6)}";

        var content = new Content
        {
            Title = request.Title,
            OriginalTitle = request.OriginalTitle,
            Slug = slug,
            Description = request.Description,
            ShortDescription = request.ShortDescription,
            Type = request.Type,
            Status = ContentStatus.Draft,
            ReleaseYear = request.ReleaseYear,
            ReleaseDate = request.ReleaseDate,
            DurationMinutes = request.DurationMinutes,
            Country = request.Country,
            Language = request.Language,
            AgeRating = request.AgeRating,
            PosterUrl = request.PosterUrl,
            BackdropUrl = request.BackdropUrl,
            TrailerUrl = request.TrailerUrl,
            VideoUrl = request.VideoUrl,
            HlsManifestUrl = request.HlsManifestUrl,
            Director = request.Director,
            Cast = request.Cast,
            IsFeatured = request.IsFeatured,
        };

        foreach (var gid in request.GenreIds.Distinct())
            content.ContentGenres.Add(new ContentGenre { ContentId = content.Id, GenreId = gid });

        _db.Contents.Add(content);
        await _db.SaveChangesAsync(cancellationToken);

        var created = await LoadWithRelationsAsync(c => c.Id == content.Id, cancellationToken);
        return Result<ContentDetailDto>.Ok(MapToDetail(created!));
    }

    public async Task<Result<ContentDetailDto>> UpdateAsync(Guid id, UpdateContentRequest request, CancellationToken cancellationToken = default)
    {
        var content = await _db.Contents
            .Include(c => c.ContentGenres)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);

        if (content is null)
            return Result<ContentDetailDto>.Fail("Content not found.");

        content.Title = request.Title;
        content.OriginalTitle = request.OriginalTitle;
        content.Description = request.Description;
        content.ShortDescription = request.ShortDescription;
        content.Type = request.Type;
        content.Status = request.Status;
        content.ReleaseYear = request.ReleaseYear;
        content.ReleaseDate = request.ReleaseDate;
        content.DurationMinutes = request.DurationMinutes;
        content.Country = request.Country;
        content.Language = request.Language;
        content.AgeRating = request.AgeRating;
        content.PosterUrl = request.PosterUrl;
        content.BackdropUrl = request.BackdropUrl;
        content.TrailerUrl = request.TrailerUrl;
        content.VideoUrl = request.VideoUrl;
        content.HlsManifestUrl = request.HlsManifestUrl;
        content.Director = request.Director;
        content.Cast = request.Cast;
        content.IsFeatured = request.IsFeatured;
        content.IsTrending = request.IsTrending;
        content.UpdatedAt = DateTime.UtcNow;

        content.ContentGenres.Clear();
        foreach (var gid in request.GenreIds.Distinct())
            content.ContentGenres.Add(new ContentGenre { ContentId = content.Id, GenreId = gid });

        await _db.SaveChangesAsync(cancellationToken);

        var updated = await LoadWithRelationsAsync(c => c.Id == id, cancellationToken);
        return Result<ContentDetailDto>.Ok(MapToDetail(updated!));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var content = await _db.Contents.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
        if (content is null)
            return Result.Fail("Content not found.");

        content.IsDeleted = true;
        content.Status = ContentStatus.Archived;
        content.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task IncrementViewCountAsync(Guid contentId, CancellationToken cancellationToken = default)
    {
        var content = await _db.Contents.FirstOrDefaultAsync(c => c.Id == contentId && !c.IsDeleted, cancellationToken);
        if (content is null) return;
        content.ViewCount++;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Content?> LoadWithRelationsAsync(
        System.Linq.Expressions.Expression<Func<Content, bool>> predicate,
        CancellationToken cancellationToken)
    {
        return await _db.Contents
            .AsNoTracking()
            .Include(c => c.ContentGenres).ThenInclude(cg => cg.Genre)
            .Include(c => c.Seasons.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Episodes.Where(e => !e.IsDeleted))
            .Where(c => !c.IsDeleted)
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    private static string GenerateSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var lower = input.Trim().ToLowerInvariant();
        var cleaned = Regex.Replace(lower, "[^a-z0-9]+", "-");
        return cleaned.Trim('-');
    }

    private static ContentListItemDto MapToListItem(Content c) => new(
        c.Id,
        c.Title,
        c.Slug,
        c.ShortDescription,
        c.Type,
        c.ReleaseYear,
        c.PosterUrl,
        c.BackdropUrl,
        c.AverageRating,
        c.ViewCount,
        c.ContentGenres.Select(cg => cg.Genre.Name).ToList());

    private static ContentDetailDto MapToDetail(Content c) => new(
        c.Id,
        c.Title,
        c.OriginalTitle,
        c.Slug,
        c.Description,
        c.Type,
        c.Status,
        c.ReleaseYear,
        c.ReleaseDate,
        c.DurationMinutes,
        c.Country,
        c.Language,
        c.AgeRating,
        c.PosterUrl,
        c.BackdropUrl,
        c.TrailerUrl,
        c.VideoUrl,
        c.HlsManifestUrl,
        c.Director,
        c.Cast,
        c.AverageRating,
        c.RatingCount,
        c.ViewCount,
        c.IsFeatured,
        c.ContentGenres
            .Select(cg => new GenreDto(cg.Genre.Id, cg.Genre.Name, cg.Genre.Slug, cg.Genre.IconUrl))
            .ToList(),
        c.Seasons
            .OrderBy(s => s.SeasonNumber)
            .Select(s => new SeasonDto(
                s.Id,
                s.SeasonNumber,
                s.Title,
                s.Description,
                s.PosterUrl,
                s.ReleaseDate,
                s.Episodes
                    .OrderBy(e => e.EpisodeNumber)
                    .Select(e => new EpisodeDto(
                        e.Id,
                        e.EpisodeNumber,
                        e.Title,
                        e.Description,
                        e.DurationMinutes,
                        e.ThumbnailUrl,
                        e.VideoUrl,
                        e.HlsManifestUrl,
                        e.ReleaseDate))
                    .ToList()))
            .ToList());
}
