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
        services.AddScoped<IAdminReviewService, AdminReviewService>();
        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<IWatchHistoryService, WatchHistoryService>();
        services.AddScoped<IWatchlistService, WatchlistService>();
        services.AddScoped<IPlaylistService, PlaylistService>();
        services.AddScoped<IMoviesAliasService, MoviesAliasService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IPaymentService, PaymentService>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
