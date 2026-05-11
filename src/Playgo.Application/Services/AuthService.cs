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
        var email = request.Email.Trim().ToLowerInvariant();
        var username = request.Username.Trim();

        if (await _db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            return Result<AuthResponse>.Fail("Email is already registered.");

        if (await _db.Users.AnyAsync(u => u.Username == username, cancellationToken))
            return Result<AuthResponse>.Fail("Username is already taken.");

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            FullName = request.FullName,
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
        var identifier = request.EmailOrUsername.Trim();
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

        user.FullName = request.FullName;
        user.AvatarUrl = request.AvatarUrl;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result<UserDto>.Ok(MapToDto(user));
    }

    private static UserDto MapToDto(User user) => new(
        user.Id,
        user.Username,
        user.Email,
        user.FullName,
        user.AvatarUrl,
        user.Role.ToString(),
        user.CreatedAt);

    private AuthResponse BuildResponse(User user, string accessToken, string refreshToken) => new(
        accessToken,
        refreshToken,
        _tokenService.GetAccessTokenExpiry(),
        MapToDto(user));

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
