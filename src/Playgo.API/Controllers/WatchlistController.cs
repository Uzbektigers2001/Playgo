using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playgo.API.Extensions;
using Playgo.Application.DTOs.UserActivity;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

[ApiController]
[Authorize]
[Route("api/watchlist")]
public class WatchlistController : ControllerBase
{
    private readonly IWatchlistService _watchlistService;

    public WatchlistController(IWatchlistService watchlistService)
    {
        _watchlistService = watchlistService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var paged = await _watchlistService.GetUserWatchlistAsync(User.GetUserId(), page, pageSize, ct);
        return Ok(paged);
    }

    [HttpPost("{contentId:guid}")]
    public async Task<IActionResult> Add(Guid contentId, [FromBody] AddToWatchlistRequest? body, CancellationToken ct)
    {
        var request = body is null
            ? new AddToWatchlistRequest(contentId, null, null)
            : new AddToWatchlistRequest(contentId, body.Priority, body.Note);

        var result = await _watchlistService.AddAsync(User.GetUserId(), request, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    [HttpDelete("{contentId:guid}")]
    public async Task<IActionResult> Remove(Guid contentId, CancellationToken ct)
    {
        var result = await _watchlistService.RemoveAsync(User.GetUserId(), contentId, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    [HttpGet("{contentId:guid}/check")]
    public async Task<IActionResult> Check(Guid contentId, CancellationToken ct)
    {
        var isInWatchlist = await _watchlistService.IsInWatchlistAsync(User.GetUserId(), contentId, ct);
        return Ok(new { isInWatchlist });
    }

    [HttpPut("{contentId:guid}/priority")]
    public async Task<IActionResult> UpdatePriority(Guid contentId, [FromBody] UpdateWatchlistPriorityRequest request, CancellationToken ct)
    {
        var result = await _watchlistService.UpdatePriorityAsync(User.GetUserId(), contentId, request, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}
