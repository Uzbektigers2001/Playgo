using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Playgo.API.Extensions;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Auth;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;

    public AuthController(IAuthService authService, ICurrentUserService currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    [HttpPost("register")]
    [EnableRateLimiting("Register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(request, ct);
        return HandleCreated(result);
    }

    [HttpPost("login")]
    [EnableRateLimiting("Login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return HandleResult(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await _authService.RefreshTokenAsync(request, ct);
        return HandleResult(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request, CancellationToken ct)
    {
        var result = await _authService.LogoutAsync(User.GetUserId(), request?.RefreshToken, ct);
        return HandleNoContent(result);
    }

    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        var result = await _authService.LogoutAllAsync(User.GetUserId(), ct);
        return HandleNoContent(result);
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
    {
        var result = await _authService.VerifyEmailAsync(request.Token, ct);
        return HandleNoContent(result);
    }

    [Authorize]
    [HttpPost("resend-verification")]
    [EnableRateLimiting("Default")]
    public async Task<IActionResult> ResendVerification(CancellationToken ct)
    {
        var result = await _authService.ResendVerificationAsync(User.GetUserId(), ct);
        return HandleNoContent(result);
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("Login")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _authService.ForgotPasswordAsync(request.Email, ct);
        return Ok(new { message = "If an account exists for that email, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("Login")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await _authService.ResetPasswordAsync(request, ct);
        return HandleNoContent(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await _authService.GetCurrentUserAsync(User.GetUserId(), ct);
        return HandleResult(result);
    }

    [Authorize]
    [HttpGet("profile")]
    public Task<IActionResult> Profile(CancellationToken ct) => Me(ct);

    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var result = await _authService.UpdateProfileAsync(User.GetUserId(), request, ct);
        return HandleResult(result);
    }

    [Authorize]
    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var result = await _authService.ChangePasswordAsync(User.GetUserId(), request, ct);
        return HandleNoContent(result);
    }

    [Authorize]
    [HttpGet("me/preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct)
    {
        var result = await _authService.GetPreferencesAsync(User.GetUserId(), ct);
        return HandleResult(result);
    }

    [Authorize]
    [HttpPut("me/preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest request, CancellationToken ct)
    {
        var result = await _authService.UpdatePreferencesAsync(User.GetUserId(), request, ct);
        return HandleResult(result);
    }

    private IActionResult HandleResult<T>(Result<T> result)
    {
        if (!result.Success) return BadRequest(new { error = result.Error });
        return Ok(result.Data);
    }

    private IActionResult HandleCreated<T>(Result<T> result)
    {
        if (!result.Success) return BadRequest(new { error = result.Error });
        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    private IActionResult HandleNoContent(Result result)
    {
        if (!result.Success) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    public record LogoutRequest(string? RefreshToken);
}
