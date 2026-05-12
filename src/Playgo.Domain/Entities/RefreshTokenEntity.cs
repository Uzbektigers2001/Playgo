using Playgo.Domain.Common;

namespace Playgo.Domain.Entities;

public class RefreshTokenEntity : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByToken { get; set; }

    public string CreatedByIp { get; set; } = string.Empty;
    public string? UserAgent { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
