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
    private readonly ILocalizationContext _localization;
    private readonly ICurrentUserService? _currentUser;
    private readonly ISubscriptionService? _subscriptionService;

    public ContentService(
        IApplicationDbContext db,
        ILocalizationContext localization,
        ICurrentUserService? currentUser = null,
        ISubscriptionService? subscriptionService = null)
    {
        _db = db;
        _localization = localization;
        _currentUser = currentUser;
        _subscriptionService = subscriptionService;
    }

    public async Task<PagedResult<ContentListItemDto>> GetContentsAsync(ContentFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

        var query = _db.Contents
            .AsNoTracking()
            .Include(c => c.ContentGenres).ThenInclude(cg => cg.Genre)
            .Include(c => c.Translations.Where(t => !t.IsDeleted))
            .Where(c => !c.IsDeleted && c.Status == ContentStatus.Published);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(c =>
                c.Title.ToLower().Contains(s) ||
                (c.OriginalTitle != null && c.OriginalTitle.ToLower().Contains(s)) ||
                c.Description.ToLower().Contains(s) ||
                c.Translations.Any(t => !t.IsDeleted && t.Title.ToLower().Contains(s)));
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

        var lang = NormalizeLang(filter.Lang) ?? _localization.CurrentLanguage;

        return new PagedResult<ContentListItemDto>
        {
            Items = items.Select(c => MapToListItem(c, lang)).ToList(),
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

        var requiresPremium = await ShouldRequirePremiumAsync(content, cancellationToken);
        return Result<ContentDetailDto>.Ok(MapToDetail(content, _localization.CurrentLanguage, requiresPremium));
    }

    public async Task<Result<ContentDetailDto>> GetContentBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var content = await LoadWithRelationsAsync(c => c.Slug == slug, cancellationToken);
        if (content is null)
            return Result<ContentDetailDto>.Fail("Content not found.");

        var requiresPremium = await ShouldRequirePremiumAsync(content, cancellationToken);
        return Result<ContentDetailDto>.Ok(MapToDetail(content, _localization.CurrentLanguage, requiresPremium));
    }

    private async Task<bool> ShouldRequirePremiumAsync(Content content, CancellationToken ct)
    {
        if (!content.IsPremium) return false;
        var userId = _currentUser?.UserId;
        if (userId is null) return true;
        if (_subscriptionService is null) return true;
        var active = await _subscriptionService.IsActiveAsync(userId.Value, ct);
        return !active;
    }

    public async Task<List<ContentListItemDto>> GetFeaturedAsync(int limit, CancellationToken cancellationToken = default)
    {
        var items = await _db.Contents
            .AsNoTracking()
            .Include(c => c.ContentGenres).ThenInclude(cg => cg.Genre)
            .Include(c => c.Translations.Where(t => !t.IsDeleted))
            .Where(c => !c.IsDeleted && c.IsFeatured && c.Status == ContentStatus.Published)
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var lang = _localization.CurrentLanguage;
        return items.Select(c => MapToListItem(c, lang)).ToList();
    }

    public async Task<List<ContentListItemDto>> GetTrendingAsync(int limit, CancellationToken cancellationToken = default)
    {
        var items = await _db.Contents
            .AsNoTracking()
            .Include(c => c.ContentGenres).ThenInclude(cg => cg.Genre)
            .Include(c => c.Translations.Where(t => !t.IsDeleted))
            .Where(c => !c.IsDeleted && c.Status == ContentStatus.Published)
            .OrderByDescending(c => c.ViewCount)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var lang = _localization.CurrentLanguage;
        return items.Select(c => MapToListItem(c, lang)).ToList();
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
            .Include(c => c.Translations.Where(t => !t.IsDeleted))
            .Where(c => !c.IsDeleted
                && c.Status == ContentStatus.Published
                && c.Id != contentId
                && c.ContentGenres.Any(cg => genreIds.Contains(cg.GenreId)))
            .OrderByDescending(c => c.AverageRating)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var lang = _localization.CurrentLanguage;
        return items.Select(c => MapToListItem(c, lang)).ToList();
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
            IsPremium = request.IsPremium,
        };

        foreach (var gid in request.GenreIds.Distinct())
            content.ContentGenres.Add(new ContentGenre { ContentId = content.Id, GenreId = gid });

        if (request.Translations is { Count: > 0 })
        {
            foreach (var t in DistinctByLang(request.Translations))
            {
                content.Translations.Add(new ContentTranslation
                {
                    ContentId = content.Id,
                    LanguageCode = NormalizeLangOrDefault(t.LanguageCode),
                    Title = t.Title,
                    OriginalTitle = t.OriginalTitle,
                    Description = t.Description,
                    ShortDescription = t.ShortDescription,
                    Director = t.Director,
                    Cast = t.Cast,
                });
            }
        }

        _db.Contents.Add(content);
        await _db.SaveChangesAsync(cancellationToken);

        var created = await LoadWithRelationsAsync(c => c.Id == content.Id, cancellationToken);
        return Result<ContentDetailDto>.Ok(MapToDetail(created!, _localization.CurrentLanguage));
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
        content.IsPremium = request.IsPremium;
        content.UpdatedAt = DateTime.UtcNow;

        content.ContentGenres.Clear();
        foreach (var gid in request.GenreIds.Distinct())
            content.ContentGenres.Add(new ContentGenre { ContentId = content.Id, GenreId = gid });

        if (request.Translations is not null)
        {
            var existing = await _db.ContentTranslations
                .Where(t => t.ContentId == id)
                .ToListAsync(cancellationToken);
            _db.ContentTranslations.RemoveRange(existing);

            foreach (var t in DistinctByLang(request.Translations))
            {
                _db.ContentTranslations.Add(new ContentTranslation
                {
                    ContentId = id,
                    LanguageCode = NormalizeLangOrDefault(t.LanguageCode),
                    Title = t.Title,
                    OriginalTitle = t.OriginalTitle,
                    Description = t.Description,
                    ShortDescription = t.ShortDescription,
                    Director = t.Director,
                    Cast = t.Cast,
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        var updated = await LoadWithRelationsAsync(c => c.Id == id, cancellationToken);
        return Result<ContentDetailDto>.Ok(MapToDetail(updated!, _localization.CurrentLanguage));
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

        var ip = _currentUser?.IpAddress;
        var ua = _currentUser?.UserAgent;
        if (ua is { Length: > 500 })
            ua = ua.Substring(0, 500);
        if (ip is { Length: > 45 })
            ip = ip.Substring(0, 45);

        _db.ContentViewLogs.Add(new ContentViewLog
        {
            ContentId = contentId,
            UserId = _currentUser?.UserId,
            ViewedAt = DateTime.UtcNow,
            IpAddress = ip,
            UserAgent = ua,
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Result<ContentTranslationDto>> UpsertTranslationAsync(Guid contentId, UpsertContentTranslationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.LanguageCode))
            return Result<ContentTranslationDto>.Fail("languageCode is required.");
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<ContentTranslationDto>.Fail("title is required.");
        if (string.IsNullOrWhiteSpace(request.Description))
            return Result<ContentTranslationDto>.Fail("description is required.");

        var contentExists = await _db.Contents.AnyAsync(c => c.Id == contentId && !c.IsDeleted, cancellationToken);
        if (!contentExists)
            return Result<ContentTranslationDto>.Fail("Content not found.");

        var lang = NormalizeLangOrDefault(request.LanguageCode);

        var existing = await _db.ContentTranslations
            .FirstOrDefaultAsync(t => t.ContentId == contentId && t.LanguageCode == lang, cancellationToken);

        if (existing is null)
        {
            existing = new ContentTranslation
            {
                ContentId = contentId,
                LanguageCode = lang,
                Title = request.Title,
                OriginalTitle = request.OriginalTitle,
                Description = request.Description,
                ShortDescription = request.ShortDescription,
                Director = request.Director,
                Cast = request.Cast,
            };
            _db.ContentTranslations.Add(existing);
        }
        else
        {
            existing.Title = request.Title;
            existing.OriginalTitle = request.OriginalTitle;
            existing.Description = request.Description;
            existing.ShortDescription = request.ShortDescription;
            existing.Director = request.Director;
            existing.Cast = request.Cast;
            existing.IsDeleted = false;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<ContentTranslationDto>.Ok(MapTranslation(existing));
    }

    public async Task<Result> DeleteTranslationAsync(Guid contentId, string languageCode, CancellationToken cancellationToken = default)
    {
        var lang = NormalizeLangOrDefault(languageCode);
        var existing = await _db.ContentTranslations
            .FirstOrDefaultAsync(t => t.ContentId == contentId && t.LanguageCode == lang && !t.IsDeleted, cancellationToken);

        if (existing is null)
            return Result.Fail("Translation not found.");

        existing.IsDeleted = true;
        existing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<List<ContentTranslationDto>> GetTranslationsAsync(Guid contentId, CancellationToken cancellationToken = default)
    {
        return await _db.ContentTranslations
            .AsNoTracking()
            .Where(t => t.ContentId == contentId && !t.IsDeleted)
            .OrderBy(t => t.LanguageCode)
            .Select(t => new ContentTranslationDto(
                t.LanguageCode,
                t.Title,
                t.OriginalTitle,
                t.Description,
                t.ShortDescription,
                t.Director,
                t.Cast))
            .ToListAsync(cancellationToken);
    }

    private async Task<Content?> LoadWithRelationsAsync(
        System.Linq.Expressions.Expression<Func<Content, bool>> predicate,
        CancellationToken cancellationToken)
    {
        return await _db.Contents
            .AsNoTracking()
            .Include(c => c.ContentGenres).ThenInclude(cg => cg.Genre)
            .Include(c => c.Translations.Where(t => !t.IsDeleted))
            .Include(c => c.Seasons.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.Episodes.Where(e => !e.IsDeleted))
            .Where(c => !c.IsDeleted)
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    private static IEnumerable<UpsertContentTranslationRequest> DistinctByLang(IEnumerable<UpsertContentTranslationRequest> items) =>
        items
            .Where(t => !string.IsNullOrWhiteSpace(t.LanguageCode))
            .GroupBy(t => NormalizeLangOrDefault(t.LanguageCode), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last());

    private static string NormalizeLangOrDefault(string? lang)
    {
        var n = NormalizeLang(lang);
        return n ?? "en";
    }

    private static string? NormalizeLang(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) return null;
        var lower = lang.Trim().ToLowerInvariant();
        return lower is "uz" or "ru" or "en" ? lower : null;
    }

    private static ContentTranslation? PickTranslation(Content c, string lang)
    {
        var match = c.Translations.FirstOrDefault(t => !t.IsDeleted && t.LanguageCode == lang);
        if (match is not null) return match;
        return c.Translations.FirstOrDefault(t => !t.IsDeleted && t.LanguageCode == "en");
    }

    private static string GenerateSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var lower = input.Trim().ToLowerInvariant();
        var cleaned = Regex.Replace(lower, "[^a-z0-9]+", "-");
        return cleaned.Trim('-');
    }

    private static ContentTranslationDto MapTranslation(ContentTranslation t) => new(
        t.LanguageCode,
        t.Title,
        t.OriginalTitle,
        t.Description,
        t.ShortDescription,
        t.Director,
        t.Cast);

    private static ContentListItemDto MapToListItem(Content c, string lang)
    {
        var tr = PickTranslation(c, lang);
        return new ContentListItemDto(
            c.Id,
            tr?.Title ?? c.Title,
            c.Slug,
            tr?.ShortDescription ?? c.ShortDescription,
            c.Type,
            c.ReleaseYear,
            c.PosterUrl,
            c.BackdropUrl,
            c.AverageRating,
            c.ViewCount,
            c.ContentGenres.Select(cg => cg.Genre.Name).ToList());
    }

    private static ContentDetailDto MapToDetail(Content c, string lang, bool requiresPremium = false)
    {
        var videoUrl = requiresPremium ? null : c.VideoUrl;
        var hlsUrl = requiresPremium ? null : c.HlsManifestUrl;
        var tr = PickTranslation(c, lang);
        return new ContentDetailDto(
            c.Id,
            tr?.Title ?? c.Title,
            tr?.OriginalTitle ?? c.OriginalTitle,
            c.Slug,
            tr?.Description ?? c.Description,
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
            videoUrl,
            hlsUrl,
            tr?.Director ?? c.Director,
            tr?.Cast ?? c.Cast,
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
                .ToList(),
            c.Translations
                .Where(t => !t.IsDeleted)
                .OrderBy(t => t.LanguageCode)
                .Select(MapTranslation)
                .ToList(),
            c.IsPremium,
            requiresPremium);
    }

    public async Task<Result<StreamUrlsDto>> GetStreamUrlsAsync(Guid contentId, CancellationToken cancellationToken = default)
    {
        var content = await _db.Contents
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contentId && !c.IsDeleted, cancellationToken);
        if (content is null)
            return Result<StreamUrlsDto>.Fail("Content not found.");

        var requiresPremium = await ShouldRequirePremiumAsync(content, cancellationToken);
        if (requiresPremium)
            return Result<StreamUrlsDto>.Fail("Premium subscription required.");

        return Result<StreamUrlsDto>.Ok(new StreamUrlsDto(content.HlsManifestUrl, content.VideoUrl));
    }
}
