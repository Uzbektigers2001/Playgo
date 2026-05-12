using Playgo.Domain.Common;

namespace Playgo.Domain.Entities;

public class ContentViewLog : BaseEntity
{
    public Guid ContentId { get; set; }
    public Content Content { get; set; } = null!;

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
