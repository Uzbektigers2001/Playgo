using Playgo.Domain.Common;

namespace Playgo.Domain.Entities;

public class Season : BaseEntity
{
    public Guid ContentId { get; set; }
    public Content Content { get; set; } = null!;

    public int SeasonNumber { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? PosterUrl { get; set; }
    public DateTime? ReleaseDate { get; set; }

    public ICollection<Episode> Episodes { get; set; } = new List<Episode>();
}

public class Episode : BaseEntity
{
    public Guid SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public int EpisodeNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DurationMinutes { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string VideoUrl { get; set; } = string.Empty;
    public string? HlsManifestUrl { get; set; }
    public DateTime? ReleaseDate { get; set; }
}
