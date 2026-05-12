using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playgo.Application.DTOs.Subscription;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

[ApiController]
[Route("api/admin/plans")]
[Authorize(Roles = "Admin")]
public class AdminPlansController : ControllerBase
{
    private readonly IPlanService _planService;

    public AdminPlansController(IPlanService planService)
    {
        _planService = planService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var plans = await _planService.GetActiveAsync(ct);
        return Ok(plans);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePlanRequest request, CancellationToken ct)
    {
        var result = await _planService.CreateAsync(request, ct);
        return result.Success ? Ok(result.Data) : BadRequest(new { error = result.Error });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePlanRequest request, CancellationToken ct)
    {
        var result = await _planService.UpdateAsync(id, request, ct);
        return result.Success ? Ok(result.Data) : NotFound(new { error = result.Error });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await _planService.DeactivateAsync(id, ct);
        return result.Success ? NoContent() : NotFound(new { error = result.Error });
    }
}
