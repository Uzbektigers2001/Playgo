using Playgo.Application.Common;
using Playgo.Application.DTOs.Admin;

namespace Playgo.Application.Services;

public interface IAdminUserService
{
    Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(AdminUserFilterRequest filter, CancellationToken ct = default);
    Task<Result<AdminUserDetailDto>> GetUserByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<AdminUserDetailDto>> UpdateAsync(Guid id, UpdateUserAsAdminRequest request, CancellationToken ct = default);
    Task<Result> BanAsync(Guid id, string reason, Guid currentAdminId, CancellationToken ct = default);
    Task<Result> UnbanAsync(Guid id, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid currentAdminId, CancellationToken ct = default);
}
