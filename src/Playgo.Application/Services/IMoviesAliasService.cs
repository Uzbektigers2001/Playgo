using Playgo.Application.DTOs.Content;

namespace Playgo.Application.Services;

public interface IMoviesAliasService
{
    Task<LegacyPagedResponse<MovieDto>> GetMoviesAsync(
        int page,
        int limit,
        string? search,
        string? genre,
        int? year,
        double? rating,
        CancellationToken cancellationToken = default);

    Task<List<MovieDto>> GetFeaturedAsync(int limit, CancellationToken cancellationToken = default);

    Task<List<MovieDto>> GetTrendingAsync(int limit, CancellationToken cancellationToken = default);

    Task<MovieDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<MovieDto>> GetRelatedAsync(Guid id, int limit, CancellationToken cancellationToken = default);
}
