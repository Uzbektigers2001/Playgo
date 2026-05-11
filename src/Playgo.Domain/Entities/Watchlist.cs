using Playgo.Domain.Common;

namespace Playgo.Domain.Entities;

public class Watchlist : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid ContentId { get; set; }
    public Content Content { get; set; } = null!;

    public int? Priority { get; set; }
    public string? Note { get; set; }
}
