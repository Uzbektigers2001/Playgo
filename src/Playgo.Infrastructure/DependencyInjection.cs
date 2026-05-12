using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Playgo.Application.Common.Interfaces;
using Playgo.Infrastructure.Identity;
using Playgo.Infrastructure.Persistence;
using Playgo.Infrastructure.Services;
using StackExchange.Redis;

namespace Playgo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = configuration["Redis:InstanceName"] ?? "playgo:";
        });

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
        services.AddScoped<ICacheService, RedisCacheService>();

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ILocalizationContext, LocalizationContext>();

        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        services.AddScoped<HangfireJobs>();

        services.AddHangfire(config =>
            config.UsePostgreSqlStorage(opts => opts.UseNpgsqlConnection(connectionString)));
        services.AddHangfireServer();

        return services;
    }
}
