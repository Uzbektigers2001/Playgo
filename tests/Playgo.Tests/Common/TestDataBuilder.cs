using Bogus;
using Playgo.Domain.Entities;
using Playgo.Domain.Enums;

namespace Playgo.Tests.Common;

/// <summary>
/// Reusable Bogus-backed builders. All instances ship with deterministic seeds via <see cref="Faker.DefaultStrictMode"/>
/// off, giving stable IDs for assertions. Override fields by passing actions.
/// </summary>
public static class TestDataBuilder
{
    public static User CreateUser(
        string? email = null,
        string? username = null,
        UserRole role = UserRole.User,
        bool emailVerified = true,
        string passwordHash = "h:test")
    {
        var faker = new Faker();
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username ?? faker.Internet.UserName(),
            Email = (email ?? faker.Internet.Email()).ToLowerInvariant(),
            FullName = faker.Name.FullName(),
            PasswordHash = passwordHash,
            Role = role,
            IsEmailVerified = emailVerified,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static Genre CreateGenre(string? name = null)
    {
        var resolved = name ?? new Faker().PickRandom("Action", "Drama", "Comedy", "Sci-Fi", "Thriller");
        return new Genre
        {
            Id = Guid.NewGuid(),
            Name = resolved,
            Slug = resolved.ToLowerInvariant().Replace(' ', '-'),
            Description = $"{resolved} content.",
        };
    }

    public static Content CreateContent(
        string? title = null,
        ContentType type = ContentType.Movie,
        ContentStatus status = ContentStatus.Published,
        bool isPremium = false,
        bool isFeatured = false,
        params Genre[] genres)
    {
        var faker = new Faker();
        var resolved = title ?? faker.Lorem.Sentence(3);
        var slug = resolved.ToLowerInvariant().Replace(' ', '-').Trim('-') + "-" + Guid.NewGuid().ToString("N")[..6];

        var content = new Content
        {
            Id = Guid.NewGuid(),
            Title = resolved,
            Slug = slug,
            Description = faker.Lorem.Paragraph(),
            ShortDescription = faker.Lorem.Sentence(),
            Type = type,
            Status = status,
            ReleaseYear = faker.Date.Past(10).Year,
            DurationMinutes = faker.Random.Int(60, 180),
            Country = faker.Address.CountryCode(),
            Language = faker.PickRandom("en", "uz", "ru"),
            VideoUrl = "https://example.com/video.mp4",
            HlsManifestUrl = "https://example.com/master.m3u8",
            PosterUrl = "https://example.com/poster.jpg",
            BackdropUrl = "https://example.com/backdrop.jpg",
            IsPremium = isPremium,
            IsFeatured = isFeatured,
            Quality = VideoQuality.HD,
            CreatedAt = DateTime.UtcNow,
        };

        foreach (var g in genres)
            content.ContentGenres.Add(new ContentGenre { ContentId = content.Id, GenreId = g.Id });

        return content;
    }

    public static Review CreateReview(Guid userId, Guid contentId, int rating = 5, string? comment = null, bool isApproved = true)
    {
        return new Review
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ContentId = contentId,
            Rating = rating,
            Comment = comment ?? new Faker().Lorem.Sentence(),
            IsApproved = isApproved,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static Plan CreatePlan(string code = "test_plan", string name = "Test Plan", decimal price = 10_000m)
    {
        return new Plan
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Description = "Test plan",
            Price = price,
            Currency = "UZS",
            Period = BillingPeriod.Monthly,
            MaxQuality = 1080,
            MaxConcurrentStreams = 1,
            HasAds = false,
            AllowsDownload = false,
            IsActive = true,
        };
    }
}
