namespace Playgo.Application.DTOs.Auth;

public record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string? FullName);

public record LoginRequest(
    string EmailOrUsername,
    string Password);

public record RefreshTokenRequest(
    string AccessToken,
    string RefreshToken);

public record UserDto(
    Guid Id,
    string Username,
    string Email,
    string? FullName,
    string? AvatarUrl,
    string Role,
    DateTime CreatedAt);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    UserDto User);

public record UpdateProfileRequest(
    string? FullName,
    string? AvatarUrl);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);
