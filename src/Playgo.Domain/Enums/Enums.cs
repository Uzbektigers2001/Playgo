namespace Playgo.Domain.Enums;

public enum ContentType
{
    Movie = 1,
    Series = 2,
    Documentary = 3,
    Anime = 4
}

public enum ContentStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
    ComingSoon = 3
}

public enum VideoQuality
{
    SD = 1,
    HD = 2,
    FullHD = 3
}

public enum UserRole
{
    User = 1,
    Moderator = 2,
    Admin = 3
}
