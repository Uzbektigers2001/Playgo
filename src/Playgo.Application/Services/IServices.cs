using Playgo.Application.Common;
using Playgo.Application.DTOs.Auth;
using Playgo.Application.DTOs.Content;
using Playgo.Domain.Entities;

namespace Playgo.Application.Services;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
}

public interface IContentService
{
    Task<PagedResult<ContentListItemDto>> GetContentsAsync(ContentFilterRequest filter, CancellationToken cancellationToken = default);
    Task<Result<ContentDetailDto>> GetContentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ContentDetailDto>> GetContentBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<List<ContentListItemDto>> GetFeaturedAsync(int limit, CancellationToken cancellationToken = default);
    Task<List<ContentListItemDto>> GetTrendingAsync(int limit, CancellationToken cancellationToken = default);
    Task<List<ContentListItemDto>> GetSimilarAsync(Guid contentId, int limit, CancellationToken cancellationToken = default);
    Task<Result<ContentDetailDto>> CreateAsync(CreateContentRequest request, CancellationToken cancellationToken = default);
    Task<Result<ContentDetailDto>> UpdateAsync(Guid id, UpdateContentRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task IncrementViewCountAsync(Guid contentId, CancellationToken cancellationToken = default);
}

public interface IGenreService
{
    Task<List<GenreDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<GenreDto>> CreateAsync(CreateGenreRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface ISeasonEpisodeService
{
    Task<Result<SeasonDto>> CreateSeasonAsync(CreateSeasonRequest request, CancellationToken cancellationToken = default);
    Task<Result<EpisodeDto>> CreateEpisodeAsync(CreateEpisodeRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteSeasonAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> DeleteEpisodeAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IReviewService
{
    Task<PagedResult<ReviewDto>> GetReviewsForContentAsync(Guid contentId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Result<ReviewDto>> CreateAsync(Guid userId, CreateReviewRequest request, CancellationToken cancellationToken = default);
    Task<Result<ReviewDto>> UpdateAsync(Guid userId, Guid reviewId, UpdateReviewRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid userId, Guid reviewId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result<ReviewVoteResultDto>> ToggleVoteAsync(Guid userId, Guid reviewId, ReviewVoteType voteType, CancellationToken cancellationToken = default);
}

public interface IAdminReviewService
{
    Task<PagedResult<ReviewDto>> GetAllAsync(AdminReviewFilter filter, CancellationToken cancellationToken = default);
    Task<Result> ApproveAsync(Guid reviewId, CancellationToken cancellationToken = default);
    Task<Result> RejectAsync(Guid reviewId, string? reason, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid reviewId, CancellationToken cancellationToken = default);
}

public interface IFavoriteService
{
    Task<PagedResult<FavoriteDto>> GetUserFavoritesAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Result> AddAsync(Guid userId, Guid contentId, CancellationToken cancellationToken = default);
    Task<Result> RemoveAsync(Guid userId, Guid contentId, CancellationToken cancellationToken = default);
    Task<bool> IsFavoriteAsync(Guid userId, Guid contentId, CancellationToken cancellationToken = default);
}

public interface IWatchHistoryService
{
    Task<PagedResult<WatchHistoryDto>> GetUserHistoryAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<List<WatchHistoryDto>> GetContinueWatchingAsync(Guid userId, int limit, CancellationToken cancellationToken = default);
    Task<Result> UpdateProgressAsync(Guid userId, UpdateWatchProgressRequest request, CancellationToken cancellationToken = default);
    Task<Result> ClearHistoryAsync(Guid userId, CancellationToken cancellationToken = default);
}
