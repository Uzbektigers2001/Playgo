using Microsoft.AspNetCore.Mvc;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

/// <summary>Legacy `/api/movies` shape used by the old frontend. Wraps `ContentService` with movie-only DTOs.</summary>
[ApiController]
[Route("api/movies")]
[Tags("Movies (Legacy)")]
[Produces("application/json")]
public class MoviesAliasController : ControllerBase
{
    private readonly IMoviesAliasService _movies;
    private readonly IContentService _contentService;

    public MoviesAliasController(IMoviesAliasService movies, IContentService contentService)
    {
        _movies = movies;
        _contentService = contentService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? genre = null,
        [FromQuery] int? year = null,
        [FromQuery] double? rating = null,
        CancellationToken ct = default)
    {
        var result = await _movies.GetMoviesAsync(page, limit, search, genre, year, rating, ct);
        return Ok(result);
    }

    [HttpGet("featured")]
    public async Task<IActionResult> Featured([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var items = await _movies.GetFeaturedAsync(limit, ct);
        return Ok(items);
    }

    [HttpGet("trending")]
    public async Task<IActionResult> Trending([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var items = await _movies.GetTrendingAsync(limit, ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var movie = await _movies.GetByIdAsync(id, ct);
        return movie is null ? NotFound(new { error = "Movie not found." }) : Ok(movie);
    }

    [HttpGet("{id:guid}/related")]
    public async Task<IActionResult> Related(Guid id, [FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var items = await _movies.GetRelatedAsync(id, limit, ct);
        return Ok(items);
    }

    [HttpPost("{id:guid}/view")]
    public async Task<IActionResult> IncrementView(Guid id, CancellationToken ct)
    {
        await _contentService.IncrementViewCountAsync(id, ct);
        return NoContent();
    }
}
