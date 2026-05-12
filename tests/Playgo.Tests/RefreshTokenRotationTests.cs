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

public class RefreshTokenRotationTests
{
    private static (AuthService Service, ApplicationDbContext Db, Queue<string> TokenQueue) Build()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"rotate-{Guid.NewGuid()}")
            .Options;
        var db = new ApplicationDbContext(options);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns<string>(p => "h:" + p);
        hasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((p, h) => h == "h:" + p);

        var queue = new Queue<string>();
        var tokens = new Mock<ITokenService>();
        tokens.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("access");
        tokens.Setup(t => t.GenerateRefreshToken()).Returns(() => queue.Count > 0 ? queue.Dequeue() : Guid.NewGuid().ToString("N"));
        tokens.Setup(t => t.GetAccessTokenExpiry()).Returns(DateTime.UtcNow.AddMinutes(15));
        tokens.Setup(t => t.GetRefreshTokenExpiry()).Returns(DateTime.UtcNow.AddDays(7));

        var service = new AuthService(db, hasher.Object, tokens.Object);
        return (service, db, queue);
    }

    [Fact]
    public async Task Refresh_RotatesToken_OldRevoked_NewIssued()
    {
        var (service, db, queue) = Build();
        queue.Enqueue("rt-1");
        queue.Enqueue("rt-2");

        var register = await service.RegisterAsync(new RegisterRequest
        {
            Email = "rot@example.com",
            Password = "password123",
            Username = "rot",
        });
        register.Success.Should().BeTrue();
        register.Data!.RefreshToken.Should().Be("rt-1");

        var refreshed = await service.RefreshTokenAsync(new RefreshTokenRequest("access", "rt-1"));
        refreshed.Success.Should().BeTrue();
        refreshed.Data!.RefreshToken.Should().Be("rt-2");

        var rt1 = await db.RefreshTokens.SingleAsync(t => t.Token == "rt-1");
        rt1.RevokedAt.Should().NotBeNull();
        rt1.ReplacedByToken.Should().Be("rt-2");

        var rt2 = await db.RefreshTokens.SingleAsync(t => t.Token == "rt-2");
        rt2.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Refresh_WithReusedRevokedToken_RevokesAllUserTokens()
    {
        var (service, db, queue) = Build();
        queue.Enqueue("rt-A");
        queue.Enqueue("rt-B");
        queue.Enqueue("rt-C");

        var register = await service.RegisterAsync(new RegisterRequest
        {
            Email = "reuse@example.com",
            Password = "password123",
            Username = "reuser",
        });
        register.Success.Should().BeTrue();

        // First rotation: A -> B
        var rotate1 = await service.RefreshTokenAsync(new RefreshTokenRequest("access", "rt-A"));
        rotate1.Success.Should().BeTrue();

        // Replay attack: try to reuse rt-A
        var replay = await service.RefreshTokenAsync(new RefreshTokenRequest("access", "rt-A"));
        replay.Success.Should().BeFalse();

        var allTokens = await db.RefreshTokens.Where(t => t.Token == "rt-A" || t.Token == "rt-B").ToListAsync();
        allTokens.Should().OnlyContain(t => t.RevokedAt != null);
    }

    [Fact]
    public async Task LogoutAll_RevokesEveryActiveTokenForUser()
    {
        var (service, db, _) = Build();

        var user = new User { Username = "logoutall", Email = "loa@example.com", PasswordHash = "h:p", Role = UserRole.User };
        db.Users.Add(user);
        var t1 = new RefreshTokenEntity { UserId = user.Id, Token = "tA", ExpiresAt = DateTime.UtcNow.AddDays(7), CreatedByIp = "127.0.0.1" };
        var t2 = new RefreshTokenEntity { UserId = user.Id, Token = "tB", ExpiresAt = DateTime.UtcNow.AddDays(7), CreatedByIp = "127.0.0.1" };
        db.RefreshTokens.Add(t1);
        db.RefreshTokens.Add(t2);
        await db.SaveChangesAsync();

        var result = await service.LogoutAllAsync(user.Id);

        result.Success.Should().BeTrue();
        var rows = await db.RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync();
        rows.Should().HaveCount(2).And.OnlyContain(t => t.RevokedAt != null);
    }

    [Fact]
    public async Task Logout_RevokesOnlyProvidedSessionToken()
    {
        var (service, db, _) = Build();

        var user = new User { Username = "logout", Email = "lo@example.com", PasswordHash = "h:p", Role = UserRole.User };
        db.Users.Add(user);
        var sessionA = new RefreshTokenEntity { UserId = user.Id, Token = "session-a", ExpiresAt = DateTime.UtcNow.AddDays(7), CreatedByIp = "127.0.0.1" };
        var sessionB = new RefreshTokenEntity { UserId = user.Id, Token = "session-b", ExpiresAt = DateTime.UtcNow.AddDays(7), CreatedByIp = "127.0.0.1" };
        db.RefreshTokens.Add(sessionA);
        db.RefreshTokens.Add(sessionB);
        await db.SaveChangesAsync();

        var result = await service.LogoutAsync(user.Id, "session-a");
        result.Success.Should().BeTrue();

        var freshA = await db.RefreshTokens.SingleAsync(t => t.Token == "session-a");
        var freshB = await db.RefreshTokens.SingleAsync(t => t.Token == "session-b");
        freshA.RevokedAt.Should().NotBeNull();
        freshB.RevokedAt.Should().BeNull();
    }
}
