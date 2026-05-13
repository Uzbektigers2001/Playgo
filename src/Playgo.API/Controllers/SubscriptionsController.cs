using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playgo.API.Extensions;
using Playgo.Application.DTOs.Subscription;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

/// <summary>Per-user subscription state. Subscribe → returns payment URL; cancel preserves remaining days.</summary>
[ApiController]
[Route("api/subscriptions")]
[Authorize]
[Tags("Subscriptions")]
[Produces("application/json")]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionsController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var result = await _subscriptionService.GetMineAsync(userId, ct);
        return result.Success ? Ok(result.Data) : NotFound(new { error = result.Error });
    }

    [HttpPost]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var result = await _subscriptionService.SubscribeAsync(userId, request, ct);
        return result.Success ? Ok(result.Data) : BadRequest(new { error = result.Error });
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var result = await _subscriptionService.CancelAsync(userId, ct);
        return result.Success ? NoContent() : BadRequest(new { error = result.Error });
    }
}
