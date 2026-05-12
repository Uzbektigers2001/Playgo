using Playgo.Domain.Common;
using Playgo.Domain.Enums;

namespace Playgo.Domain.Entities;

public class Content : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string? OriginalTitle { get; set; }
    public string? ShortDescription { get; set; }
    public string? Country { get; set; }
    public string? Language { get; set; }
    public string? AgeRating { get; set; }

    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string? TrailerUrl { get; set; }
    public string? VideoUrl { get; set; }
    public string? HlsManifestUrl { get; set; }

    public string? Director { get; set; }
    public string? Cast { get; set; }

    public ContentType Type { get; set; }
    public ContentStatus Status { get; set; } = ContentStatus.Draft;

    public int? ReleaseYear { get; set; }
    public int? DurationMinutes { get; set; }
    public DateTime? ReleaseDate { get; set; }

    public double AverageRating { get; set; } = 0;
    public int RatingCount { get; set; } = 0;
    public long ViewCount { get; set; } = 0;

    public bool IsFeatured { get; set; } = false;
    public bool IsTrending { get; set; } = false;

    public ICollection<ContentGenre> ContentGenres { get; set; } = new List<ContentGenre>();
    public ICollection<Season> Seasons { get; set; } = new List<Season>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<WatchHistory> WatchHistories { get; set; } = new List<WatchHistory>();
    public ICollection<ContentTranslation> Translations { get; set; } = new List<ContentTranslation>();
}
