using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playgo.API.Extensions;
using Playgo.Application.DTOs.Content;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

/// <summary>Public review listing per content + create / update / delete / vote (toggle semantics).</summary>
[ApiController]
[Route("api/reviews")]
[Tags("Reviews")]
[Produces("application/json")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [HttpGet("content/{contentId:guid}")]
    public async Task<IActionResult> GetForContent(
        Guid contentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var paged = await _reviewService.GetReviewsForContentAsync(contentId, page, pageSize, ct);
        return Ok(paged);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest request, CancellationToken ct)
    {
        var result = await _reviewService.CreateAsync(User.GetUserId(), request, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReviewRequest request, CancellationToken ct)
    {
        var result = await _reviewService.UpdateAsync(User.GetUserId(), id, request, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return Ok(result.Data);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var isAdmin = string.Equals(User.GetRole(), "Admin", StringComparison.Ordinal);
        var result = await _reviewService.DeleteAsync(User.GetUserId(), id, isAdmin, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:guid}/vote")]
    public async Task<IActionResult> Vote(Guid id, [FromBody] ToggleVoteRequest request, CancellationToken ct)
    {
        var result = await _reviewService.ToggleVoteAsync(User.GetUserId(), id, request.VoteType, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return Ok(result.Data);
    }
}
