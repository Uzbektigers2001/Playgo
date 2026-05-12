using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Playgo.Application.Services;

namespace Playgo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IContentService, ContentService>();
        services.AddScoped<IGenreService, GenreService>();
        services.AddScoped<ISeasonEpisodeService, SeasonEpisodeService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<IWatchHistoryService, WatchHistoryService>();
        services.AddScoped<IWatchlistService, WatchlistService>();
        services.AddScoped<IPlaylistService, PlaylistService>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
