using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Playgo.Application.Common.Interfaces;
using Playgo.Infrastructure.Persistence;

namespace Playgo.Tests.Common;

/// <summary>
/// Boots the real ASP.NET pipeline against an in-memory EF Core database (per-factory unique).
/// Redis cache, Hangfire server, and other infra side effects are stubbed so tests don't need Docker.
/// </summary>
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"playgo-test-{Guid.NewGuid():N}";

    /// <summary>Hook called once after the host is built so individual tests can pre-seed.</summary>
    public Action<ApplicationDbContext>? Seed { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["JwtSettings:Secret"] = "TEST_SECRET_KEY_AT_LEAST_32_CHARS_LONG_FOR_TESTS_ONLY",
                ["JwtSettings:Issuer"] = "PlaygoTest",
                ["JwtSettings:Audience"] = "PlaygoTestUsers",
                ["JwtSettings:AccessTokenMinutes"] = "60",
                ["JwtSettings:RefreshTokenDays"] = "7",
                ["AllowedOrigins:0"] = "http://localhost:3000",
                ["Seed:AdminEmail"] = "admin@playgo.uz",
                ["Seed:AdminPassword"] = "Admin123!",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace EF Core DbContext with InMemory.
            var efDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                            d.ServiceType == typeof(ApplicationDbContext))
                .ToList();
            foreach (var d in efDescriptors) services.Remove(d);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Replace IApplicationDbContext to point at the same context.
            services.RemoveAll<IApplicationDbContext>();
            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

            // Replace ICacheService with a no-op so we don't need Redis.
            services.RemoveAll<ICacheService>();
            services.AddSingleton<ICacheService, NoopCacheService>();

            // Suppress noisy logs in test runs.
            services.AddLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Warning);
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        Seed?.Invoke(db);

        return host;
    }

    public ApplicationDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    /// <summary>Trivial in-process cache stub so the cache contract is satisfied without Redis.</summary>
    private sealed class NoopCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class => Task.FromResult<T?>(null);
        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) => Task.CompletedTask;
    }
}
