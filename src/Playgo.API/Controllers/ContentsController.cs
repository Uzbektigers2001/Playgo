using Microsoft.AspNetCore.Mvc;
using Playgo.Application.DTOs.Content;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

[ApiController]
[Route("api/contents")]
public class ContentsController : ControllerBase
{
    private readonly IContentService _contentService;

    public ContentsController(IContentService contentService)
    {
        _contentService = contentService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] ContentFilterRequest filter, CancellationToken ct)
    {
        var paged = await _contentService.GetContentsAsync(filter, ct);
        return Ok(paged);
    }

    [HttpGet("featured")]
    public async Task<IActionResult> Featured([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var items = await _contentService.GetFeaturedAsync(limit, ct);
        return Ok(items);
    }

    [HttpGet("trending")]
    public async Task<IActionResult> Trending([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var items = await _contentService.GetTrendingAsync(limit, ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _contentService.GetContentByIdAsync(id, ct);
        return result.Success ? Ok(result.Data) : NotFound(new { error = result.Error });
    }

    [HttpGet("slug/{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
    {
        var result = await _contentService.GetContentBySlugAsync(slug, ct);
        return result.Success ? Ok(result.Data) : NotFound(new { error = result.Error });
    }

    [HttpGet("{id:guid}/similar")]
    public async Task<IActionResult> Similar(Guid id, [FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var items = await _contentService.GetSimilarAsync(id, limit, ct);
        return Ok(items);
    }

    [HttpPost("{id:guid}/view")]
    public async Task<IActionResult> IncrementView(Guid id, CancellationToken ct)
    {
        await _contentService.IncrementViewCountAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/translations")]
    public async Task<IActionResult> Translations(Guid id, CancellationToken ct)
    {
        var items = await _contentService.GetTranslationsAsync(id, ct);
        return Ok(items);
    }
}
