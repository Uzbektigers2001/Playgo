using Playgo.Domain.Common;
using Playgo.Domain.Enums;

namespace Playgo.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? RefreshToken { get; set; }

    public UserRole Role { get; set; } = UserRole.User;
    public bool IsEmailVerified { get; set; } = false;

    public DateTime? RefreshTokenExpiry { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<WatchHistory> WatchHistories { get; set; } = new List<WatchHistory>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
