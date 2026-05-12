using Playgo.Domain.Common;

namespace Playgo.Domain.Entities;

public class Playlist : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; } = false;
    public string? CoverImageUrl { get; set; }

    public ICollection<PlaylistItem> Items { get; set; } = new List<PlaylistItem>();
}

public class PlaylistItem : BaseEntity
{
    public Guid PlaylistId { get; set; }
    public Playlist Playlist { get; set; } = null!;

    public Guid ContentId { get; set; }
    public Content Content { get; set; } = null!;

    public int OrderIndex { get; set; }
}
