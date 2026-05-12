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

public class AuthServiceTests
{
    private static (AuthService Service, ApplicationDbContext Db) Build()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"auth-{Guid.NewGuid()}")
            .Options;
        var db = new ApplicationDbContext(options);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns<string>(p => "h:" + p);
        hasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((p, h) => h == "h:" + p);

        var tokens = new Mock<ITokenService>();
        tokens.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("access-token");
        tokens.Setup(t => t.GenerateRefreshToken()).Returns(() => Guid.NewGuid().ToString("N"));
        tokens.Setup(t => t.GetAccessTokenExpiry()).Returns(DateTime.UtcNow.AddMinutes(15));
        tokens.Setup(t => t.GetRefreshTokenExpiry()).Returns(DateTime.UtcNow.AddDays(7));

        return (new AuthService(db, hasher.Object, tokens.Object), db);
    }

    [Fact]
    public async Task Register_NewEmail_ReturnsTokens_AndCreatesPreferences()
    {
        var (svc, db) = Build();

        var result = await svc.RegisterAsync(new RegisterRequest
        {
            Email = "Foo@Example.COM",
            Password = "password123",
            Username = "fooUser",
        });

        result.Success.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("access-token");
        var user = await db.Users.SingleAsync();
        user.Email.Should().Be("foo@example.com"); // normalized
        user.Username.Should().Be("fooUser");
        (await db.UserPreferences.AnyAsync(p => p.UserId == user.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Register_DuplicateEmail_Fails()
    {
        var (svc, db) = Build();
        db.Users.Add(new User { Email = "dup@example.com", Username = "dup", PasswordHash = "h:x" });
        await db.SaveChangesAsync();

        var result = await svc.RegisterAsync(new RegisterRequest { Email = "dup@example.com", Password = "password123" });

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("already registered");
    }

    [Fact]
    public async Task Register_ShortPassword_Fails()
    {
        var (svc, _) = Build();

        var result = await svc.RegisterAsync(new RegisterRequest { Email = "a@b.c", Password = "12345" });

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("at least 6");
    }

    [Fact]
    public async Task Login_WithEmail_Succeeds()
    {
        var (svc, db) = Build();
        db.Users.Add(new User { Email = "e@x.com", Username = "user1", PasswordHash = "h:p", Role = UserRole.User });
        await db.SaveChangesAsync();

        var result = await svc.LoginAsync(new LoginRequest { Email = "e@x.com", Password = "p" });

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Login_WithUsername_Succeeds()
    {
        var (svc, db) = Build();
        db.Users.Add(new User { Email = "e@x.com", Username = "myname", PasswordHash = "h:p", Role = UserRole.User });
        await db.SaveChangesAsync();

        var result = await svc.LoginAsync(new LoginRequest { EmailOrUsername = "myname", Password = "p" });
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Login_WrongPassword_Fails()
    {
        var (svc, db) = Build();
        db.Users.Add(new User { Email = "e@x.com", Username = "u", PasswordHash = "h:right", Role = UserRole.User });
        await db.SaveChangesAsync();

        var result = await svc.LoginAsync(new LoginRequest { Email = "e@x.com", Password = "wrong" });

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Invalid credentials");
    }

    [Fact]
    public async Task RefreshToken_Valid_RotatesToken()
    {
        var (svc, db) = Build();
        var user = new User
        {
            Email = "r@x.com",
            Username = "r",
            PasswordHash = "h:p",
            RefreshToken = "old-token",
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(7),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await svc.RefreshTokenAsync(new RefreshTokenRequest("access", "old-token"));

        result.Success.Should().BeTrue();
        result.Data!.RefreshToken.Should().NotBe("old-token");
    }

    [Fact]
    public async Task RefreshToken_Expired_Fails()
    {
        var (svc, db) = Build();
        db.Users.Add(new User
        {
            Email = "r@x.com",
            Username = "r",
            PasswordHash = "h:p",
            RefreshToken = "expired-token",
            RefreshTokenExpiry = DateTime.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();

        var result = await svc.RefreshTokenAsync(new RefreshTokenRequest("access", "expired-token"));

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Logout_ClearsRefreshToken()
    {
        var (svc, db) = Build();
        var user = new User { Email = "l@x.com", Username = "l", PasswordHash = "h:p", RefreshToken = "rt" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await svc.LogoutAsync(user.Id);

        result.Success.Should().BeTrue();
        var fresh = await db.Users.SingleAsync();
        fresh.RefreshToken.Should().BeNull();
        fresh.RefreshTokenExpiry.Should().BeNull();
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_Fails()
    {
        var (svc, db) = Build();
        var user = new User { Email = "c@x.com", Username = "c", PasswordHash = "h:correct" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await svc.ChangePasswordAsync(user.Id, new ChangePasswordRequest("wrong", "newpw"));

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateProfile_PersistsChanges()
    {
        var (svc, db) = Build();
        var user = new User { Email = "p@x.com", Username = "p", PasswordHash = "h:p", FullName = "Old Name" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await svc.UpdateProfileAsync(user.Id, new UpdateProfileRequest("New Name", null, null, "https://avatar"));

        result.Success.Should().BeTrue();
        var fresh = await db.Users.SingleAsync();
        fresh.FullName.Should().Be("New Name");
        fresh.AvatarUrl.Should().Be("https://avatar");
    }
}
