using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playgo.Application.DTOs.Content;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

[ApiController]
[Route("api/genres")]
public class GenresController : ControllerBase
{
    private readonly IGenreService _genreService;

    public GenresController(IGenreService genreService)
    {
        _genreService = genreService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var genres = await _genreService.GetAllAsync(ct);
        return Ok(genres);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGenreRequest request, CancellationToken ct)
    {
        var result = await _genreService.CreateAsync(request, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _genreService.DeleteAsync(id, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    [HttpGet("{genreId:guid}/translations")]
    public async Task<IActionResult> GetTranslations(Guid genreId, CancellationToken ct)
    {
        var items = await _genreService.GetTranslationsAsync(genreId, ct);
        return Ok(items);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{genreId:guid}/translations")]
    public async Task<IActionResult> UpsertTranslation(
        Guid genreId,
        [FromBody] UpsertGenreTranslationRequest request,
        CancellationToken ct)
    {
        var result = await _genreService.UpsertTranslationAsync(genreId, request, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return Ok(result.Data);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{genreId:guid}/translations/{lang}")]
    public async Task<IActionResult> DeleteTranslation(Guid genreId, string lang, CancellationToken ct)
    {
        var result = await _genreService.DeleteTranslationAsync(genreId, lang, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}
