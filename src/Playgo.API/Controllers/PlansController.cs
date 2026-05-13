using Microsoft.AspNetCore.Mvc;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

/// <summary>Public read-only catalog of subscription plans. Cached in Redis for 1 hour.</summary>
[ApiController]
[Route("api/plans")]
[Tags("Plans")]
[Produces("application/json")]
public class PlansController : ControllerBase
{
    private readonly IPlanService _planService;

    public PlansController(IPlanService planService)
    {
        _planService = planService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var plans = await _planService.GetActiveAsync(ct);
        return Ok(plans);
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetByCode(string code, CancellationToken ct)
    {
        var result = await _planService.GetByCodeAsync(code, ct);
        return result.Success ? Ok(result.Data) : NotFound(new { error = result.Error });
    }
}
