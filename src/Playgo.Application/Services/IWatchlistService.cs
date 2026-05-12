using Playgo.Application.Common;
using Playgo.Application.DTOs.UserActivity;

namespace Playgo.Application.Services;

public interface IWatchlistService
{
    Task<PagedResult<WatchlistItemDto>> GetUserWatchlistAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Result> AddAsync(Guid userId, AddToWatchlistRequest request, CancellationToken cancellationToken = default);
    Task<Result> RemoveAsync(Guid userId, Guid contentId, CancellationToken cancellationToken = default);
    Task<bool> IsInWatchlistAsync(Guid userId, Guid contentId, CancellationToken cancellationToken = default);
    Task<Result> UpdatePriorityAsync(Guid userId, Guid contentId, UpdateWatchlistPriorityRequest request, CancellationToken cancellationToken = default);
}
