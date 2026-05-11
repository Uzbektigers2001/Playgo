using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playgo.Application.Common;
using Playgo.Application.DTOs.Content;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

[ApiController]
[Route("api/admin/contents")]
[Authorize(Roles = "Admin")]
public class AdminContentsController : ControllerBase
{
    private readonly IContentService _contentService;
    private readonly ISeasonEpisodeService _seasonEpisodeService;

    public AdminContentsController(
        IContentService contentService,
        ISeasonEpisodeService seasonEpisodeService)
    {
        _contentService = contentService;
        _seasonEpisodeService = seasonEpisodeService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContentRequest request, CancellationToken ct)
    {
        var result = await _contentService.CreateAsync(request, ct);
        return HandleCreated(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContentRequest request, CancellationToken ct)
    {
        var result = await _contentService.UpdateAsync(id, request, ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _contentService.DeleteAsync(id, ct);
        return HandleNoContent(result);
    }

    [HttpPost("{contentId:guid}/seasons")]
    public async Task<IActionResult> CreateSeason(Guid contentId, [FromBody] CreateSeasonRequest request, CancellationToken ct)
    {
        var effective = request with { ContentId = contentId };
        var result = await _seasonEpisodeService.CreateSeasonAsync(effective, ct);
        return HandleCreated(result);
    }

    [HttpPost("seasons/{seasonId:guid}/episodes")]
    public async Task<IActionResult> CreateEpisode(Guid seasonId, [FromBody] CreateEpisodeRequest request, CancellationToken ct)
    {
        var effective = request with { SeasonId = seasonId };
        var result = await _seasonEpisodeService.CreateEpisodeAsync(effective, ct);
        return HandleCreated(result);
    }

    [HttpDelete("seasons/{seasonId:guid}")]
    public async Task<IActionResult> DeleteSeason(Guid seasonId, CancellationToken ct)
    {
        var result = await _seasonEpisodeService.DeleteSeasonAsync(seasonId, ct);
        return HandleNoContent(result);
    }

    [HttpDelete("episodes/{episodeId:guid}")]
    public async Task<IActionResult> DeleteEpisode(Guid episodeId, CancellationToken ct)
    {
        var result = await _seasonEpisodeService.DeleteEpisodeAsync(episodeId, ct);
        return HandleNoContent(result);
    }

    private IActionResult HandleResult<T>(Result<T> result)
    {
        if (!result.Success) return BadRequest(new { error = result.Error });
        return Ok(result.Data);
    }

    private IActionResult HandleCreated<T>(Result<T> result)
    {
        if (!result.Success) return BadRequest(new { error = result.Error });
        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    private IActionResult HandleNoContent(Result result)
    {
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}
