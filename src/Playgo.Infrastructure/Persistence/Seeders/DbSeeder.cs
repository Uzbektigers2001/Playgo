using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Playgo.Application.Common.Interfaces;
using Playgo.Domain.Entities;
using Playgo.Domain.Enums;

namespace Playgo.Infrastructure.Persistence.Seeders;

/// <summary>
/// Idempotent database seeder.
///
/// SeedAdmin / SeedGenres / SeedPlans run in every environment. SeedDemoContent only runs in
/// Development or Staging — production never gets fixture content.
/// </summary>
public class DbSeeder
{
    private static readonly string[] DefaultGenres =
    {
        "Action", "Drama", "Comedy", "Sci-Fi", "Horror", "Thriller",
        "Romance", "Animation", "Documentary", "Adventure", "Crime", "Fantasy",
    };

    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(
        ApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<DbSeeder> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedAdminAsync(ct);
        await SeedGenresAsync(ct);
        await SeedPlansAsync(ct);

        if (_environment.IsDevelopment() || string.Equals(_environment.EnvironmentName, "Staging", StringComparison.OrdinalIgnoreCase))
            await SeedDemoContentAsync(ct);
    }

    public async Task SeedAdminAsync(CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(u => u.Role == UserRole.Admin && !u.IsDeleted, ct))
            return;

        var email = (_configuration["Seed:AdminEmail"] ?? "admin@playgo.uz").Trim().ToLowerInvariant();
        var password = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD")
            ?? _configuration["Seed:AdminPassword"]
            ?? "Admin123!";

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            _logger.LogInformation("Seed: admin email {Email} already exists, skipping admin seed.", email);
            return;
        }

        var admin = new User
        {
            Username = "admin",
            Email = email,
            FullName = "Playgo Admin",
            PasswordHash = _passwordHasher.HashPassword(password),
            Role = UserRole.Admin,
            IsEmailVerified = true,
        };

        _db.Users.Add(admin);
        _db.UserPreferences.Add(new UserPreferences
        {
            UserId = admin.Id,
            Language = PreferredLanguage.En,
            Quality = PreferredQuality.Auto,
            Autoplay = true,
            EmailNotifications = true,
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seed: admin user created ({Email}).", email);
    }

    public async Task SeedGenresAsync(CancellationToken ct = default)
    {
        var existing = await _db.Genres
            .Select(g => g.Name)
            .ToListAsync(ct);

        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var name in DefaultGenres)
        {
            if (existingSet.Contains(name)) continue;

            _db.Genres.Add(new Genre
            {
                Name = name,
                Slug = name.ToLowerInvariant().Replace(' ', '-').Replace("/", "-"),
                Description = $"{name} content.",
            });
            added++;
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seed: {Count} genres added.", added);
        }
    }

    public async Task SeedPlansAsync(CancellationToken ct = default)
    {
        if (await _db.Plans.AnyAsync(ct))
            return;

        _db.Plans.AddRange(
            new Plan
            {
                Code = "free",
                Name = "Free",
                Description = "Free plan with ads and limited quality.",
                Price = 0m,
                Currency = "UZS",
                Period = BillingPeriod.Monthly,
                MaxQuality = 720,
                MaxConcurrentStreams = 1,
                HasAds = true,
                AllowsDownload = false,
                FeaturesJson = "[\"HD 720p\",\"Reklama bilan\",\"1 ekran\"]",
            },
            new Plan
            {
                Code = "premium_monthly",
                Name = "Premium Monthly",
                Description = "Premium content, 4K, no ads, downloads.",
                Price = 49_000m,
                Currency = "UZS",
                Period = BillingPeriod.Monthly,
                MaxQuality = 2160,
                MaxConcurrentStreams = 2,
                HasAds = false,
                AllowsDownload = true,
                FeaturesJson = "[\"4K Ultra HD\",\"Reklamasiz\",\"2 ekran\",\"Offline yuklab olish\"]",
            },
            new Plan
            {
                Code = "premium_yearly",
                Name = "Premium Yearly",
                Description = "All Premium features, billed yearly.",
                Price = 490_000m,
                Currency = "UZS",
                Period = BillingPeriod.Yearly,
                MaxQuality = 2160,
                MaxConcurrentStreams = 4,
                HasAds = false,
                AllowsDownload = true,
                FeaturesJson = "[\"4K Ultra HD\",\"Reklamasiz\",\"4 ekran\",\"Offline yuklab olish\",\"Yillik chegirma\"]",
            });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seed: 3 default plans inserted.");
    }

    public async Task SeedDemoContentAsync(CancellationToken ct = default)
    {
        if (await _db.Contents.AnyAsync(ct))
            return;

        var genres = await _db.Genres.ToListAsync(ct);
        if (genres.Count == 0)
        {
            _logger.LogWarning("Seed: cannot seed demo content — no genres present.");
            return;
        }

        var rng = new Random(42);

        var titles = new (string Title, ContentType Type)[]
        {
            ("Kechki Quyosh", ContentType.Movie),
            ("Toshkentdan Sevgi", ContentType.Movie),
            ("Yulduzlar Ortida", ContentType.Movie),
            ("Cho'lda Yo'qolgan", ContentType.Movie),
            ("Oxirgi Detektiv", ContentType.Series),
            ("Kvant Aktsiyasi", ContentType.Series),
            ("Tog' Hikoyalari", ContentType.Documentary),
            ("Nano-Sayyoh", ContentType.Anime),
            ("Vaqt Lavhasi", ContentType.Movie),
            ("Soya Pulsi", ContentType.Series),
        };

        const string lorem = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. " +
                             "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. " +
                             "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi.";

        for (var i = 0; i < titles.Length; i++)
        {
            var (title, type) = titles[i];
            var slug = SlugFrom(title) + "-" + (i + 1);
            var year = 2018 + (i % 8);

            var content = new Content
            {
                Title = title,
                OriginalTitle = title,
                Slug = slug,
                Description = lorem,
                ShortDescription = $"Demo {type.ToString().ToLowerInvariant()} #{i + 1}.",
                Type = type,
                Status = ContentStatus.Published,
                ReleaseYear = year,
                ReleaseDate = new DateTime(year, ((i % 12) + 1), 1, 0, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 60 + i * 7,
                Country = i % 2 == 0 ? "UZ" : "US",
                Language = i % 2 == 0 ? "uz" : "en",
                AgeRating = i % 3 == 0 ? "PG-13" : "R",
                PosterUrl = $"https://placehold.co/400x600?text={Uri.EscapeDataString(title)}",
                BackdropUrl = $"https://placehold.co/1280x720?text={Uri.EscapeDataString(title)}",
                TrailerUrl = "https://example.com/trailer.mp4",
                VideoUrl = "https://example.com/video.mp4",
                HlsManifestUrl = "https://example.com/master.m3u8",
                Director = "Demo Director",
                Cast = "Actor One, Actor Two, Actor Three",
                IsFeatured = i < 3,
                IsTrending = i >= 5 && i < 8,
                IsPremium = i % 5 == 0 && i > 0,
                Quality = VideoQuality.HD,
                AverageRating = 6.0 + (i % 4) * 0.8,
                RatingCount = 10 + i * 3,
                ViewCount = 100 + i * 250,
            };

            var pickCount = 1 + rng.Next(3);
            var picks = genres.OrderBy(_ => rng.Next()).Take(pickCount).ToList();
            foreach (var g in picks)
                content.ContentGenres.Add(new ContentGenre { ContentId = content.Id, GenreId = g.Id });

            _db.Contents.Add(content);
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seed: {Count} demo contents inserted.", titles.Length);
    }

    private static string SlugFrom(string input)
    {
        var lower = input.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder(lower.Length);
        foreach (var c in lower)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (char.IsWhiteSpace(c) || c == '-' || c == '_') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }
}
