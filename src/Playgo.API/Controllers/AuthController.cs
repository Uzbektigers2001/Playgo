using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Playgo.API.Extensions;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Auth;
using Playgo.Application.Services;

namespace Playgo.API.Controllers;

/// <summary>
/// Authentication and account management.
/// </summary>
[ApiController]
[Route("api/auth")]
[Tags("Auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;

    /// <summary>Constructs the controller; dependencies are injected by ASP.NET Core.</summary>
    public AuthController(IAuthService authService, ICurrentUserService currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    /// <summary>Foydalanuvchini ro'yxatdan o'tkazadi va JWT tokenlarni qaytaradi.</summary>
    /// <param name="request">Ro'yxatdan o'tish ma'lumotlari (email, password, ixtiyoriy username/fullName).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Successful registration. Returns access + refresh tokens and user profile.</response>
    /// <response code="400">Validation failed yoki email/username band.</response>
    [HttpPost("register")]
    [EnableRateLimiting("Register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(request, ct);
        return HandleCreated(result);
    }

    /// <summary>Email/username + parol bilan kirish va JWT olish.</summary>
    /// <param name="request">Login credentials. `emailOrUsername` yoki `email`.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Login muvaffaqiyatli; access + refresh tokens qaytariladi.</response>
    /// <response code="400">Invalid credentials.</response>
    [HttpPost("login")]
    [EnableRateLimiting("Login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return HandleResult(result);
    }

    /// <summary>Yangi access + refresh token chiqaradi (rotation).</summary>
    /// <response code="200">Tokenlar yangilandi.</response>
    /// <response code="400">Refresh token noto'g'ri yoki muddati o'tgan.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await _authService.RefreshTokenAsync(request, ct);
        return HandleResult(result);
    }

    /// <summary>Faqat shu sessiyaning refresh tokenini bekor qiladi.</summary>
    /// <response code="204">Logout muvaffaqiyatli.</response>
    /// <response code="401">Authorize talab qilinadi.</response>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request, CancellationToken ct)
    {
        var result = await _authService.LogoutAsync(User.GetUserId(), request?.RefreshToken, ct);
        return HandleNoContent(result);
    }

    /// <summary>Userning hamma faol refresh tokenlarini revoke qiladi.</summary>
    [Authorize]
    [HttpPost("logout-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        var result = await _authService.LogoutAllAsync(User.GetUserId(), ct);
        return HandleNoContent(result);
    }

    /// <summary>Email tasdiqlash tokenini tekshiradi.</summary>
    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
    {
        var result = await _authService.VerifyEmailAsync(request.Token, ct);
        return HandleNoContent(result);
    }

    /// <summary>Verification emailni qayta yuboradi.</summary>
    [Authorize]
    [HttpPost("resend-verification")]
    [EnableRateLimiting("Default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResendVerification(CancellationToken ct)
    {
        var result = await _authService.ResendVerificationAsync(User.GetUserId(), ct);
        return HandleNoContent(result);
    }

    /// <summary>Parol tiklash linkini yuboradi (har doim 200 — email enumeration himoyasi).</summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("Login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _authService.ForgotPasswordAsync(request.Email, ct);
        return Ok(new { message = "If an account exists for that email, a reset link has been sent." });
    }

    /// <summary>Parolni reset tokeni bilan o'zgartiradi.</summary>
    [HttpPost("reset-password")]
    [EnableRateLimiting("Login")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await _authService.ResetPasswordAsync(request, ct);
        return HandleNoContent(result);
    }

    /// <summary>Joriy foydalanuvchining profili.</summary>
    /// <response code="200">User DTO.</response>
    /// <response code="401">Authorize talab qilinadi.</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await _authService.GetCurrentUserAsync(User.GetUserId(), ct);
        return HandleResult(result);
    }

    /// <summary>`/me` ning eski aliasi (legacy frontend compat).</summary>
    [Authorize]
    [HttpGet("profile")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Profile(CancellationToken ct) => Me(ct);

    /// <summary>Joriy foydalanuvchining profilini yangilaydi.</summary>
    [Authorize]
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var result = await _authService.UpdateProfileAsync(User.GetUserId(), request, ct);
        return HandleResult(result);
    }

    /// <summary>Parolni o'zgartiradi (joriy parol kerak).</summary>
    [Authorize]
    [HttpPut("me/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var result = await _authService.ChangePasswordAsync(User.GetUserId(), request, ct);
        return HandleNoContent(result);
    }

    /// <summary>Foydalanuvchi sozlamalari (til, sifat, autoplay, notification).</summary>
    [Authorize]
    [HttpGet("me/preferences")]
    [ProducesResponseType(typeof(UserPreferencesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPreferences(CancellationToken ct)
    {
        var result = await _authService.GetPreferencesAsync(User.GetUserId(), ct);
        return HandleResult(result);
    }

    /// <summary>Sozlamalarni PATCH-semantikasi bilan yangilaydi.</summary>
    [Authorize]
    [HttpPut("me/preferences")]
    [ProducesResponseType(typeof(UserPreferencesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
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
