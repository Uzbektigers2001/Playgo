using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Playgo.API.Extensions;
using Playgo.Application.Services;
using Playgo.Domain.Entities;

namespace Playgo.API.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IServiceProvider _serviceProvider;

    public PaymentsController(IPaymentService paymentService, IServiceProvider serviceProvider)
    {
        _paymentService = paymentService;
        _serviceProvider = serviceProvider;
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var result = await _paymentService.GetMineAsync(userId, page, pageSize, ct);
        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin")]
    public async Task<IActionResult> AdminAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _paymentService.AdminGetAllAsync(page, pageSize, ct);
        return Ok(result);
    }

    public record CallbackBody(Guid PaymentId, string? TransactionId);

    [HttpPost("callback/{provider}")]
    public async Task<IActionResult> Callback(string provider, CancellationToken ct)
    {
        if (!Enum.TryParse<PaymentProvider>(provider, true, out var providerEnum))
            return BadRequest(new { error = "Unknown provider." });

        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());

        var paymentProvider = _serviceProvider.GetRequiredKeyedService<IPaymentProviderService>(providerEnum.ToString());
        var verified = await paymentProvider.VerifyCallbackAsync(rawBody, headers, ct);
        if (!verified)
            return Unauthorized(new { error = "Signature verification failed." });

        CallbackBody? body = null;
        if (!string.IsNullOrWhiteSpace(rawBody))
        {
            try
            {
                body = JsonSerializer.Deserialize<CallbackBody>(rawBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                body = null;
            }
        }

        if (body is null || body.PaymentId == Guid.Empty)
            return BadRequest(new { error = "paymentId is required." });

        var result = await _paymentService.MarkCompletedAsync(body.PaymentId, body.TransactionId, rawBody, ct);
        return result.Success ? Ok(new { status = "completed" }) : NotFound(new { error = result.Error });
    }
}
