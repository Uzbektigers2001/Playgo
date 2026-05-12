using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Playgo.Application.Common.Interfaces;
using Playgo.Infrastructure.Identity;
using Playgo.Infrastructure.Persistence;
using Playgo.Infrastructure.Persistence.Seeders;
using Playgo.Application.Services;
using Playgo.Infrastructure.Services;
using Playgo.Infrastructure.Services.Payments;
using StackExchange.Redis;

namespace Playgo.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Wires Infrastructure services. When <paramref name="environment"/> is the "Testing" host environment,
    /// Redis and Hangfire are skipped so integration tests can run without external services.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        var isTesting = environment?.IsEnvironment("Testing") ?? false;

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        if (!isTesting)
        {
            var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = configuration["Redis:InstanceName"] ?? "playgo:";
            });

            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
            services.AddScoped<ICacheService, RedisCacheService>();
        }

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ILocalizationContext, LocalizationContext>();

        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        services.AddKeyedScoped<IPaymentProviderService, ClickPaymentProvider>("Click");
        services.AddKeyedScoped<IPaymentProviderService, PaymePaymentProvider>("Payme");
        services.AddKeyedScoped<IPaymentProviderService, StripePaymentProvider>("Stripe");
        services.AddKeyedScoped<IPaymentProviderService, ManualPaymentProvider>("Manual");

        services.AddScoped<HangfireJobs>();
        services.AddScoped<DbSeeder>();

        if (!isTesting)
        {
            services.AddHangfire(config =>
                config.UsePostgreSqlStorage(opts => opts.UseNpgsqlConnection(connectionString)));
            services.AddHangfireServer();
        }

        return services;
    }
}
