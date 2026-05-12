using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playgo.Application.DTOs.Content;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

[ApiController]
[Route("api/admin/reviews")]
[Authorize(Roles = "Admin")]
public class AdminReviewsController : ControllerBase
{
    private readonly IAdminReviewService _adminReviews;

    public AdminReviewsController(IAdminReviewService adminReviews)
    {
        _adminReviews = adminReviews;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? contentId = null,
        [FromQuery] bool? isApproved = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var paged = await _adminReviews.GetAllAsync(new AdminReviewFilter
        {
            Page = page,
            PageSize = pageSize,
            ContentId = contentId,
            IsApproved = isApproved,
            UserId = userId,
            Search = search,
        }, ct);
        return Ok(paged);
    }

    [HttpPut("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var result = await _adminReviews.ApproveAsync(id, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    [HttpPut("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectReviewRequest request, CancellationToken ct)
    {
        var result = await _adminReviews.RejectAsync(id, request.Reason, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _adminReviews.DeleteAsync(id, ct);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}
