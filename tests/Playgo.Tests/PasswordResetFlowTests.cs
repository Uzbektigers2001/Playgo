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

public class PasswordResetFlowTests
{
    private static (AuthService Service, ApplicationDbContext Db, Mock<IEmailService> Email, Mock<IPasswordHasher> Hasher) Build()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"reset-{Guid.NewGuid()}")
            .Options;
        var db = new ApplicationDbContext(options);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns<string>(p => "h:" + p);
        hasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((p, h) => h == "h:" + p);

        var tokens = new Mock<ITokenService>();
        tokens.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("access");
        tokens.Setup(t => t.GenerateRefreshToken()).Returns(() => Guid.NewGuid().ToString("N"));
        tokens.Setup(t => t.GetAccessTokenExpiry()).Returns(DateTime.UtcNow.AddMinutes(15));
        tokens.Setup(t => t.GetRefreshTokenExpiry()).Returns(DateTime.UtcNow.AddDays(7));

        var email = new Mock<IEmailService>();
        var service = new AuthService(db, hasher.Object, tokens.Object, emailService: email.Object);
        return (service, db, email, hasher);
    }

    [Fact]
    public async Task ForgotPassword_NonExistentEmail_StillReturnsOk_NoEmailSent()
    {
        var (service, _, email, _) = Build();

        var result = await service.ForgotPasswordAsync("nobody@example.com");

        result.Success.Should().BeTrue();
        email.Verify(e => e.SendTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_ExistingUser_GeneratesTokenAndSendsEmail()
    {
        var (service, db, email, _) = Build();
        db.Users.Add(new User { Username = "alice", Email = "alice@example.com", PasswordHash = "h:old", Role = UserRole.User });
        await db.SaveChangesAsync();

        var result = await service.ForgotPasswordAsync("alice@example.com");

        result.Success.Should().BeTrue();
        var user = await db.Users.SingleAsync(u => u.Email == "alice@example.com");
        user.PasswordResetToken.Should().NotBeNullOrEmpty();
        user.PasswordResetTokenExpiry.Should().BeAfter(DateTime.UtcNow).And.BeBefore(DateTime.UtcNow.AddHours(2));

        email.Verify(e => e.SendTemplateAsync(
                "alice@example.com",
                "password-reset",
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_UpdatesPasswordAndRevokesTokens()
    {
        var (service, db, _, hasher) = Build();
        var user = new User
        {
            Username = "bob",
            Email = "bob@example.com",
            PasswordHash = "h:oldpass",
            PasswordResetToken = "rstok-1",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30),
            RefreshToken = "old-rt",
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(7),
            Role = UserRole.User,
        };
        user.RefreshTokens.Add(new RefreshTokenEntity
        {
            UserId = user.Id,
            Token = "old-rt",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByIp = "127.0.0.1",
        });
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await service.ResetPasswordAsync(new ResetPasswordRequest("rstok-1", "newpass123"));

        result.Success.Should().BeTrue();
        var fresh = await db.Users.Include(u => u.RefreshTokens).SingleAsync(u => u.Id == user.Id);
        fresh.PasswordHash.Should().Be("h:newpass123");
        fresh.PasswordResetToken.Should().BeNull();
        fresh.PasswordResetTokenExpiry.Should().BeNull();
        fresh.RefreshToken.Should().BeNull();
        fresh.RefreshTokens.Should().OnlyContain(t => t.RevokedAt != null);
    }

    [Fact]
    public async Task ResetPassword_WithExpiredToken_Fails()
    {
        var (service, db, _, _) = Build();
        db.Users.Add(new User
        {
            Username = "carol",
            Email = "carol@example.com",
            PasswordHash = "h:pw",
            PasswordResetToken = "expired",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(-1),
            Role = UserRole.User,
        });
        await db.SaveChangesAsync();

        var result = await service.ResetPasswordAsync(new ResetPasswordRequest("expired", "newpass123"));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("expired");
    }
}
