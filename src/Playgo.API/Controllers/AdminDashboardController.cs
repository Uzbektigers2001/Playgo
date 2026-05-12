using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

/// <summary>Admin dashboard analytics. `/stats` is cached in Redis for 60 seconds.</summary>
[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin")]
[Tags("Admin - Dashboard")]
[Produces("application/json")]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _service;

    public AdminDashboardController(IAdminDashboardService service)
    {
        _service = service;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var stats = await _service.GetStatsAsync(ct);
        return Ok(stats);
    }
}
