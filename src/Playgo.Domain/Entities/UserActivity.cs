using Playgo.Domain.Common;

namespace Playgo.Domain.Entities;

public class Favorite : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid ContentId { get; set; }
    public Content Content { get; set; } = null!;
}

public class WatchHistory : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid ContentId { get; set; }
    public Content Content { get; set; } = null!;

    public Guid? EpisodeId { get; set; }
    public Episode? Episode { get; set; }

    public int PositionSeconds { get; set; }
    public int DurationSeconds { get; set; }
    public bool IsCompleted { get; set; } = false;
    public DateTime LastWatchedAt { get; set; } = DateTime.UtcNow;
}

public class Review : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid ContentId { get; set; }
    public Content Content { get; set; } = null!;

    public int Rating { get; set; }
    public string? Comment { get; set; }
    public int LikesCount { get; set; } = 0;
    public bool IsApproved { get; set; } = true;
}
