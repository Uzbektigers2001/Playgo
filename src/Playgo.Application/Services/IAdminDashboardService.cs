using Playgo.Application.DTOs.Admin;

namespace Playgo.Application.Services;

public interface IAdminDashboardService
{
    Task<AdminDashboardStatsDto> GetStatsAsync(CancellationToken ct = default);
}
