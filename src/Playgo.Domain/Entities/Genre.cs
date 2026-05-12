using Playgo.Domain.Common;

namespace Playgo.Domain.Entities;

public class Genre : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }

    public ICollection<ContentGenre> ContentGenres { get; set; } = new List<ContentGenre>();
    public ICollection<GenreTranslation> Translations { get; set; } = new List<GenreTranslation>();
}

public class ContentGenre
{
    public Guid ContentId { get; set; }
    public Content Content { get; set; } = null!;

    public Guid GenreId { get; set; }
    public Genre Genre { get; set; } = null!;
}
