namespace Playgo.Application.DTOs.Admin;

public record AdminDashboardStatsDto(
    int TotalUsers,
    int NewUsersThisMonth,
    int TotalContents,
    int PublishedContents,
    int DraftContents,
    long TotalViews,
    int TotalReviews,
    double AverageRating,
    int TotalFavorites,
    int TotalWatchlists,
    List<AdminRecentItemDto> RecentMovies,
    List<AdminRecentItemDto> RecentUsers,
    List<AdminGenreStatDto> TopGenres,
    List<AdminViewsByDayDto> ViewsLast30Days);

public record AdminRecentItemDto(
    Guid Id,
    string Title,
    string? Subtitle,
    DateTime CreatedAt);

public record AdminGenreStatDto(
    Guid GenreId,
    string Name,
    int ContentCount,
    long TotalViews);

public record AdminViewsByDayDto(
    string Date,
    long Views);
