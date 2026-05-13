using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playgo.API.Extensions;
using Playgo.Application.Common;
using Playgo.Application.DTOs.UserActivity;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

/// <summary>User-owned playlists; public playlists visible to everyone.</summary>
[ApiController]
[Route("api/playlists")]
[Tags("Playlists")]
[Produces("application/json")]
public class PlaylistsController : ControllerBase
{
    private readonly IPlaylistService _playlistService;

    public PlaylistsController(IPlaylistService playlistService)
    {
        _playlistService = playlistService;
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var paged = await _playlistService.GetMineAsync(User.GetUserId(), page, pageSize, ct);
        return Ok(paged);
    }

    [HttpGet("public")]
    public async Task<IActionResult> GetPublic(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var paged = await _playlistService.GetPublicAsync(page, pageSize, search, ct);
        return Ok(paged);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        Guid? currentUserId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : null;
        var result = await _playlistService.GetByIdAsync(id, currentUserId, ct);
        return HandleResult(result);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePlaylistRequest request, CancellationToken ct)
    {
        var result = await _playlistService.CreateAsync(User.GetUserId(), request, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePlaylistRequest request, CancellationToken ct)
    {
        var result = await _playlistService.UpdateAsync(User.GetUserId(), id, request, ct);
        return HandleResult(result);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _playlistService.DeleteAsync(User.GetUserId(), id, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:guid}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddItemToPlaylistRequest request, CancellationToken ct)
    {
        var result = await _playlistService.AddItemAsync(User.GetUserId(), id, request, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    [Authorize]
    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid id, Guid itemId, CancellationToken ct)
    {
        var result = await _playlistService.RemoveItemAsync(User.GetUserId(), id, itemId, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    [Authorize]
    [HttpPut("{id:guid}/reorder")]
    public async Task<IActionResult> Reorder(Guid id, [FromBody] ReorderPlaylistRequest request, CancellationToken ct)
    {
        var result = await _playlistService.ReorderItemsAsync(User.GetUserId(), id, request, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    private IActionResult HandleResult<T>(Result<T> result)
    {
        if (!result.Success) return BadRequest(new { error = result.Error });
        return Ok(result.Data);
    }
}
