using Playgo.Application.Common;
using Playgo.Application.DTOs.UserActivity;

namespace Playgo.Application.Services;

public interface IPlaylistService
{
    Task<Result<PlaylistDto>> CreateAsync(Guid userId, CreatePlaylistRequest request, CancellationToken cancellationToken = default);
    Task<Result<PlaylistDto>> UpdateAsync(Guid userId, Guid playlistId, UpdatePlaylistRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid userId, Guid playlistId, CancellationToken cancellationToken = default);

    Task<PagedResult<PlaylistDto>> GetMineAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<PlaylistDto>> GetPublicAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default);
    Task<Result<PlaylistDetailDto>> GetByIdAsync(Guid playlistId, Guid? currentUserId, CancellationToken cancellationToken = default);

    Task<Result<PlaylistItemDto>> AddItemAsync(Guid userId, Guid playlistId, AddItemToPlaylistRequest request, CancellationToken cancellationToken = default);
    Task<Result> RemoveItemAsync(Guid userId, Guid playlistId, Guid itemId, CancellationToken cancellationToken = default);
    Task<Result> ReorderItemsAsync(Guid userId, Guid playlistId, ReorderPlaylistRequest request, CancellationToken cancellationToken = default);
}
