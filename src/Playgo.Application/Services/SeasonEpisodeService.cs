using Microsoft.EntityFrameworkCore;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Content;
using Playgo.Domain.Entities;

namespace Playgo.Application.Services;

public class SeasonEpisodeService : ISeasonEpisodeService
{
    private readonly IApplicationDbContext _db;

    public SeasonEpisodeService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<SeasonDto>> CreateSeasonAsync(CreateSeasonRequest request, CancellationToken cancellationToken = default)
    {
        var contentExists = await _db.Contents.AnyAsync(c => c.Id == request.ContentId && !c.IsDeleted, cancellationToken);
        if (!contentExists)
            return Result<SeasonDto>.Fail("Content not found.");

        var season = new Season
        {
            ContentId = request.ContentId,
            SeasonNumber = request.SeasonNumber,
            Title = request.Title,
            Description = request.Description,
            PosterUrl = request.PosterUrl,
            ReleaseDate = request.ReleaseDate,
        };

        _db.Seasons.Add(season);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<SeasonDto>.Ok(new SeasonDto(
            season.Id,
            season.SeasonNumber,
            season.Title,
            season.Description,
            season.PosterUrl,
            season.ReleaseDate,
            new List<EpisodeDto>()));
    }

    public async Task<Result<EpisodeDto>> CreateEpisodeAsync(CreateEpisodeRequest request, CancellationToken cancellationToken = default)
    {
        var seasonExists = await _db.Seasons.AnyAsync(s => s.Id == request.SeasonId && !s.IsDeleted, cancellationToken);
        if (!seasonExists)
            return Result<EpisodeDto>.Fail("Season not found.");

        var episode = new Episode
        {
            SeasonId = request.SeasonId,
            EpisodeNumber = request.EpisodeNumber,
            Title = request.Title,
            Description = request.Description,
            DurationMinutes = request.DurationMinutes,
            ThumbnailUrl = request.ThumbnailUrl,
            VideoUrl = request.VideoUrl,
            HlsManifestUrl = request.HlsManifestUrl,
            ReleaseDate = request.ReleaseDate,
        };

        _db.Episodes.Add(episode);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<EpisodeDto>.Ok(new EpisodeDto(
            episode.Id,
            episode.EpisodeNumber,
            episode.Title,
            episode.Description,
            episode.DurationMinutes,
            episode.ThumbnailUrl,
            episode.VideoUrl,
            episode.HlsManifestUrl,
            episode.ReleaseDate));
    }

    public async Task<Result> DeleteSeasonAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var season = await _db.Seasons.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
        if (season is null)
            return Result.Fail("Season not found.");

        season.IsDeleted = true;
        season.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> DeleteEpisodeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var episode = await _db.Episodes.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, cancellationToken);
        if (episode is null)
            return Result.Fail("Episode not found.");

        episode.IsDeleted = true;
        episode.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
