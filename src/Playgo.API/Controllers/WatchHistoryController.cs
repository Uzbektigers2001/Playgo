using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playgo.API.Extensions;
using Playgo.Application.DTOs.Content;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

[ApiController]
[Authorize]
[Route("api/watch-history")]
public class WatchHistoryController : ControllerBase
{
    private readonly IWatchHistoryService _watchHistoryService;

    public WatchHistoryController(IWatchHistoryService watchHistoryService)
    {
        _watchHistoryService = watchHistoryService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var paged = await _watchHistoryService.GetUserHistoryAsync(User.GetUserId(), page, pageSize, ct);
        return Ok(paged);
    }

    [HttpGet("continue-watching")]
    public async Task<IActionResult> ContinueWatching([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var items = await _watchHistoryService.GetContinueWatchingAsync(User.GetUserId(), limit, ct);
        return Ok(items);
    }

    [HttpPut("progress")]
    public async Task<IActionResult> UpdateProgress([FromBody] UpdateWatchProgressRequest request, CancellationToken ct)
    {
        var result = await _watchHistoryService.UpdateProgressAsync(User.GetUserId(), request, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        var result = await _watchHistoryService.ClearHistoryAsync(User.GetUserId(), ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}
