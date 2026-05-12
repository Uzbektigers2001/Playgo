using Playgo.Domain.Enums;

namespace Playgo.Application.DTOs.Admin;

public record AdminUserListItemDto(
    Guid Id,
    string Username,
    string Email,
    string? FullName,
    string? AvatarUrl,
    UserRole Role,
    bool IsBanned,
    bool IsEmailVerified,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    int ReviewsCount,
    int FavoritesCount);

public record AdminUserDetailDto(
    Guid Id,
    string Username,
    string Email,
    string? FullName,
    string? AvatarUrl,
    UserRole Role,
    bool IsBanned,
    bool IsEmailVerified,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    int ReviewsCount,
    int FavoritesCount,
    string? BanReason,
    DateTime? BannedAt,
    int WatchlistCount,
    int PlaylistsCount,
    List<AdminUserActivityDto> RecentActivity);

public record AdminUserActivityDto(
    string Type,
    Guid? ContentId,
    string? ContentTitle,
    DateTime OccurredAt);

public class AdminUserFilterRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public UserRole? Role { get; set; }
    public bool? IsBanned { get; set; }
    public bool? IsEmailVerified { get; set; }
    public string? SortBy { get; set; }
}

public record UpdateUserAsAdminRequest(
    string? FullName,
    string? AvatarUrl,
    UserRole? Role,
    bool? IsBanned,
    string? BanReason);

public record BanUserRequest(string Reason);
