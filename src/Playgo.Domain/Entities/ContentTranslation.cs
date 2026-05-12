using Playgo.Domain.Common;

namespace Playgo.Domain.Entities;

public class ContentTranslation : BaseEntity
{
    public Guid ContentId { get; set; }
    public Content Content { get; set; } = null!;

    public string LanguageCode { get; set; } = "en";

    public string Title { get; set; } = string.Empty;
    public string? OriginalTitle { get; set; }

    public string Description { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }

    public string? Director { get; set; }
    public string? Cast { get; set; }
}
