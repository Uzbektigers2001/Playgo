using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playgo.API.Extensions;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

[ApiController]
[Authorize]
[Route("api/favorites")]
public class FavoritesController : ControllerBase
{
    private readonly IFavoriteService _favoriteService;

    public FavoritesController(IFavoriteService favoriteService)
    {
        _favoriteService = favoriteService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var paged = await _favoriteService.GetUserFavoritesAsync(User.GetUserId(), page, pageSize, ct);
        return Ok(paged);
    }

    [HttpPost("{contentId:guid}")]
    public async Task<IActionResult> Add(Guid contentId, CancellationToken ct)
    {
        var result = await _favoriteService.AddAsync(User.GetUserId(), contentId, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    [HttpDelete("{contentId:guid}")]
    public async Task<IActionResult> Remove(Guid contentId, CancellationToken ct)
    {
        var result = await _favoriteService.RemoveAsync(User.GetUserId(), contentId, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    [HttpGet("{contentId:guid}/check")]
    public async Task<IActionResult> Check(Guid contentId, CancellationToken ct)
    {
        var isFavorite = await _favoriteService.IsFavoriteAsync(User.GetUserId(), contentId, ct);
        return Ok(new { isFavorite });
    }
}
