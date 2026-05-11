using Playgo.Domain.Common;

namespace Playgo.Domain.Entities;

public class GenreTranslation : BaseEntity
{
    public Guid GenreId { get; set; }
    public Genre Genre { get; set; } = null!;

    public string LanguageCode { get; set; } = "en";

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
