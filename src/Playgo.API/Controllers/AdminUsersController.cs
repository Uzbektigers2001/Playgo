using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playgo.API.Extensions;
using Playgo.Application.DTOs.Admin;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _service;

    public AdminUsersController(IAdminUserService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] AdminUserFilterRequest filter, CancellationToken ct)
    {
        var paged = await _service.GetUsersAsync(filter, ct);
        return Ok(paged);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetUserByIdAsync(id, ct);
        return result.Success ? Ok(result.Data) : NotFound(new { error = result.Error });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserAsAdminRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        return result.Success ? Ok(result.Data) : BadRequest(new { error = result.Error });
    }

    [HttpPost("{id:guid}/ban")]
    public async Task<IActionResult> Ban(Guid id, [FromBody] BanUserRequest request, CancellationToken ct)
    {
        var currentAdminId = User.GetUserId();
        var result = await _service.BanAsync(id, request.Reason, currentAdminId, ct);
        return result.Success ? NoContent() : BadRequest(new { error = result.Error });
    }

    [HttpPost("{id:guid}/unban")]
    public async Task<IActionResult> Unban(Guid id, CancellationToken ct)
    {
        var result = await _service.UnbanAsync(id, ct);
        return result.Success ? NoContent() : BadRequest(new { error = result.Error });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var currentAdminId = User.GetUserId();
        var result = await _service.DeleteAsync(id, currentAdminId, ct);
        return result.Success ? NoContent() : BadRequest(new { error = result.Error });
    }
}
