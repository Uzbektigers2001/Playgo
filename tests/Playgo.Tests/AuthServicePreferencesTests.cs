using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Auth;
using Playgo.Application.Services;
using Playgo.Domain.Entities;
using Playgo.Domain.Enums;
using Playgo.Infrastructure.Persistence;

namespace Playgo.Tests;

public class AuthServicePreferencesTests
{
    private static (AuthService Service, ApplicationDbContext Db, User User) BuildSubject(bool seedPreferences)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"prefs-{Guid.NewGuid()}")
            .Options;

        var db = new ApplicationDbContext(options);

        var user = new User
        {
            Username = "alice",
            Email = "alice@example.com",
            PasswordHash = "hash",
            Role = UserRole.User,
        };
        db.Users.Add(user);

        if (seedPreferences)
        {
            db.UserPreferences.Add(new UserPreferences
            {
                UserId = user.Id,
                Language = PreferredLanguage.En,
                Quality = PreferredQuality.Auto,
                Autoplay = true,
                EmailNotifications = true,
                PushNotifications = false,
            });
        }

        db.SaveChanges();

        var hasher = new Mock<IPasswordHasher>().Object;
        var tokens = new Mock<ITokenService>().Object;
        var service = new AuthService(db, hasher, tokens);
        return (service, db, user);
    }

    [Fact]
    public async Task DefaultPreferences_AreCreated_OnGetWhenAbsent()
    {
        var (service, db, user) = BuildSubject(seedPreferences: false);

        var before = await db.UserPreferences.CountAsync();
        before.Should().Be(0);

        var result = await service.GetPreferencesAsync(user.Id);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Language.Should().Be("En");
        result.Data.Quality.Should().Be("Auto");
        result.Data.Autoplay.Should().BeTrue();
        result.Data.EmailNotifications.Should().BeTrue();
        result.Data.PushNotifications.Should().BeFalse();

        var after = await db.UserPreferences.CountAsync();
        after.Should().Be(1);

        var row = await db.UserPreferences.SingleAsync();
        row.UserId.Should().Be(user.Id);
        row.Language.Should().Be(PreferredLanguage.En);
        row.Quality.Should().Be(PreferredQuality.Auto);
    }

    [Fact]
    public async Task UpdatePreferences_OnlyUpdatesProvidedFields()
    {
        var (service, db, user) = BuildSubject(seedPreferences: true);

        var request = new UpdatePreferencesRequest(
            Language: "Uz",
            Quality: "HD720",
            Autoplay: false,
            EmailNotifications: null,
            PushNotifications: null);

        var result = await service.UpdatePreferencesAsync(user.Id, request);

        result.Success.Should().BeTrue();
        result.Data!.Language.Should().Be("Uz");
        result.Data.Quality.Should().Be("HD720");
        result.Data.Autoplay.Should().BeFalse();
        result.Data.EmailNotifications.Should().BeTrue();
        result.Data.PushNotifications.Should().BeFalse();

        var row = await db.UserPreferences.SingleAsync(p => p.UserId == user.Id);
        row.Language.Should().Be(PreferredLanguage.Uz);
        row.Quality.Should().Be(PreferredQuality.HD720);
        row.Autoplay.Should().BeFalse();
        row.EmailNotifications.Should().BeTrue();
        row.PushNotifications.Should().BeFalse();
    }

    [Fact]
    public async Task InvalidEnumValue_ReturnsFailure()
    {
        var (service, db, user) = BuildSubject(seedPreferences: true);

        var bad = new UpdatePreferencesRequest(
            Language: "French",
            Quality: null,
            Autoplay: null,
            EmailNotifications: null,
            PushNotifications: null);

        var result = await service.UpdatePreferencesAsync(user.Id, bad);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Invalid language");

        var row = await db.UserPreferences.SingleAsync(p => p.UserId == user.Id);
        row.Language.Should().Be(PreferredLanguage.En);
    }
}
