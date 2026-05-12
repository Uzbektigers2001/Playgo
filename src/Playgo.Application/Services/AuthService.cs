using System.Text;
using Microsoft.EntityFrameworkCore;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Auth;
using Playgo.Domain.Entities;
using Playgo.Domain.Enums;

namespace Playgo.Application.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<AuthResponse>.Fail("Email is required.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return Result<AuthResponse>.Fail("Password must be at least 6 characters.");

        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            return Result<AuthResponse>.Fail("Email is already registered.");

        var baseUsername = !string.IsNullOrWhiteSpace(request.Username)
            ? request.Username.Trim()
            : !string.IsNullOrWhiteSpace(request.Name)
                ? GenerateUsernameFromName(request.Name)
                : GenerateUsernameFromName(email.Split('@')[0]);

        if (string.IsNullOrWhiteSpace(baseUsername))
            baseUsername = "user";

        var username = baseUsername;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (!await _db.Users.AnyAsync(u => u.Username == username, cancellationToken))
                break;
            username = baseUsername + Random.Shared.Next(10, 9999);
            if (attempt == 4)
                return Result<AuthResponse>.Fail("Could not allocate a unique username, please try again.");
        }

        var fullName = !string.IsNullOrWhiteSpace(request.FullName)
            ? request.FullName
            : request.Name;

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            FullName = fullName,
            Role = UserRole.User,
        };

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = _tokenService.GetRefreshTokenExpiry();
        user.LastLoginAt = DateTime.UtcNow;

        _db.Users.Add(user);
        _db.UserPreferences.Add(new UserPreferences
        {
            UserId = user.Id,
            Language = PreferredLanguage.En,
            Quality = PreferredQuality.Auto,
            Autoplay = true,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return Result<AuthResponse>.Ok(BuildResponse(user, accessToken, refreshToken));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var identifierRaw = request.GetIdentifier();
        if (string.IsNullOrWhiteSpace(identifierRaw))
            return Result<AuthResponse>.Fail("Invalid credentials.");

        var identifier = identifierRaw.Trim();
        var emailLower = identifier.ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(
            u => !u.IsDeleted && (u.Email == emailLower || u.Username == identifier),
            cancellationToken);

        if (user is null)
            return Result<AuthResponse>.Fail("Invalid credentials.");

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            return Result<AuthResponse>.Fail("Invalid credentials.");

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = _tokenService.GetRefreshTokenExpiry();
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Result<AuthResponse>.Ok(BuildResponse(user, accessToken, refreshToken));
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(
            u => !u.IsDeleted && u.RefreshToken == request.RefreshToken,
            cancellationToken);

        if (user is null || user.RefreshTokenExpiry is null || user.RefreshTokenExpiry <= DateTime.UtcNow)
            return Result<AuthResponse>.Fail("Invalid or expired refresh token.");

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = _tokenService.GetRefreshTokenExpiry();
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Result<AuthResponse>.Ok(BuildResponse(user, accessToken, refreshToken));
    }

    public async Task<Result> LogoutAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);
        if (user is null)
            return Result.Fail("User not found.");

        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<UserDto>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(u => u.Preferences)
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);
        if (user is null)
            return Result<UserDto>.Fail("User not found.");

        return Result<UserDto>.Ok(MapToDto(user));
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);
        if (user is null)
            return Result.Fail("User not found.");

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            return Result.Fail("Current password is incorrect.");

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<UserDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);
        if (user is null)
            return Result<UserDto>.Fail("User not found.");

        var (existingFirst, existingLast) = SplitFullName(user.FullName);
        var firstName = !string.IsNullOrWhiteSpace(request.FirstName) ? request.FirstName : existingFirst;
        var lastName = !string.IsNullOrWhiteSpace(request.LastName) ? request.LastName : existingLast;

        var fullName = !string.IsNullOrWhiteSpace(request.FullName)
            ? request.FullName
            : JoinName(firstName, lastName) ?? user.FullName;

        user.FullName = fullName;
        user.AvatarUrl = request.AvatarUrl ?? user.AvatarUrl;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result<UserDto>.Ok(MapToDto(user));
    }

    private static UserDto MapToDto(User user)
    {
        var (firstName, lastName) = SplitFullName(user.FullName);
        return new UserDto(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            firstName,
            lastName,
            user.AvatarUrl,
            user.AvatarUrl,
            user.Role.ToString().ToLowerInvariant(),
            user.CreatedAt,
            user.UpdatedAt);
    }

    private AuthResponse BuildResponse(User user, string accessToken, string refreshToken) => new()
    {
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        AccessTokenExpiry = _tokenService.GetAccessTokenExpiry(),
        User = MapToDto(user),
    };

    private static string GenerateUsernameFromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var lower = name.Trim().ToLowerInvariant();
        var sb = new StringBuilder(lower.Length);
        foreach (var c in lower)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (char.IsWhiteSpace(c)) sb.Append('_');
        }

        var cleaned = sb.ToString().Trim('_');
        if (cleaned.Length == 0) return string.Empty;
        return cleaned.Length > 20 ? cleaned.Substring(0, 20) : cleaned;
    }

    private static (string? First, string? Last) SplitFullName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return (null, null);

        var trimmed = fullName.Trim();
        var spaceIdx = trimmed.IndexOf(' ');
        if (spaceIdx < 0) return (trimmed, null);

        var first = trimmed.Substring(0, spaceIdx);
        var rest = trimmed.Substring(spaceIdx + 1).Trim();
        return (first, string.IsNullOrEmpty(rest) ? null : rest);
    }

    private static string? JoinName(string? first, string? last)
    {
        var f = string.IsNullOrWhiteSpace(first) ? null : first.Trim();
        var l = string.IsNullOrWhiteSpace(last) ? null : last.Trim();
        if (f is null && l is null) return null;
        if (f is null) return l;
        if (l is null) return f;
        return $"{f} {l}";
    }

    public async Task<Result<UserPreferencesDto>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(u => u.Preferences)
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);
        if (user is null)
            return Result<UserPreferencesDto>.Fail("User not found.");

        if (user.Preferences is null)
        {
            user.Preferences = new UserPreferences
            {
                UserId = user.Id,
                Language = PreferredLanguage.En,
                Quality = PreferredQuality.Auto,
                Autoplay = true,
                EmailNotifications = true,
                PushNotifications = false,
            };
            _db.UserPreferences.Add(user.Preferences);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result<UserPreferencesDto>.Ok(MapPreferences(user.Preferences));
    }

    public async Task<Result<UserPreferencesDto>> UpdatePreferencesAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken cancellationToken = default)
    {
        var userExists = await _db.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);
        if (!userExists)
            return Result<UserPreferencesDto>.Fail("User not found.");

        var prefs = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        var isNew = false;
        if (prefs is null)
        {
            prefs = new UserPreferences
            {
                UserId = userId,
                Language = PreferredLanguage.En,
                Quality = PreferredQuality.Auto,
                Autoplay = true,
                EmailNotifications = true,
                PushNotifications = false,
            };
            isNew = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            if (!Enum.TryParse<PreferredLanguage>(request.Language, ignoreCase: true, out var lang))
                return Result<UserPreferencesDto>.Fail($"Invalid language value: '{request.Language}'.");
            prefs.Language = lang;
        }

        if (!string.IsNullOrWhiteSpace(request.Quality))
        {
            if (!Enum.TryParse<PreferredQuality>(request.Quality, ignoreCase: true, out var quality))
                return Result<UserPreferencesDto>.Fail($"Invalid quality value: '{request.Quality}'.");
            prefs.Quality = quality;
        }

        if (request.Autoplay.HasValue) prefs.Autoplay = request.Autoplay.Value;
        if (request.EmailNotifications.HasValue) prefs.EmailNotifications = request.EmailNotifications.Value;
        if (request.PushNotifications.HasValue) prefs.PushNotifications = request.PushNotifications.Value;

        if (isNew) _db.UserPreferences.Add(prefs);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<UserPreferencesDto>.Ok(MapPreferences(prefs));
    }

    private static UserPreferencesDto MapPreferences(UserPreferences prefs) => new(
        prefs.Language.ToString(),
        prefs.Quality.ToString(),
        prefs.Autoplay,
        prefs.EmailNotifications,
        prefs.PushNotifications);
}
