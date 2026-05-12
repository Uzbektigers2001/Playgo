using System.Text.Json.Serialization;

namespace Playgo.Application.DTOs.Auth;

public class LoginRequest
{
    public string? EmailOrUsername { get; set; }
    public string? Email { get; set; }
    public string Password { get; set; } = string.Empty;

    public string GetIdentifier() =>
        !string.IsNullOrWhiteSpace(EmailOrUsername) ? EmailOrUsername!
        : !string.IsNullOrWhiteSpace(Email) ? Email!
        : string.Empty;
}

public class RegisterRequest
{
    public string? Username { get; set; }
    public string? Name { get; set; }
    public string? FullName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public record RefreshTokenRequest(
    string AccessToken,
    string RefreshToken);

public record UserDto(
    Guid Id,
    string Username,
    string Email,
    string? FullName,
    string? FirstName,
    string? LastName,
    string? AvatarUrl,
    string? Avatar,
    string Role,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    [JsonInclude]
    public string Token => AccessToken;

    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiry { get; set; }
    public UserDto User { get; set; } = null!;
}

public record UpdateProfileRequest(
    string? FullName,
    string? FirstName,
    string? LastName,
    string? AvatarUrl);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public record UserPreferencesDto(
    string Language,
    string Quality,
    bool Autoplay,
    bool EmailNotifications,
    bool PushNotifications);

public record UpdatePreferencesRequest(
    string? Language,
    string? Quality,
    bool? Autoplay,
    bool? EmailNotifications,
    bool? PushNotifications);

public record VerifyEmailRequest(string Token);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);
