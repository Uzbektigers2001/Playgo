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

public class EmailVerificationFlowTests
{
    private static (AuthService Service, ApplicationDbContext Db, Mock<IEmailService> Email) Build()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"verify-{Guid.NewGuid()}")
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
        return (service, db, email);
    }

    [Fact]
    public async Task Register_GeneratesVerificationTokenAndSendsEmail()
    {
        var (service, db, email) = Build();

        var result = await service.RegisterAsync(new RegisterRequest
        {
            Email = "newuser@example.com",
            Password = "password123",
            Username = "newuser",
        });

        result.Success.Should().BeTrue();
        var user = await db.Users.SingleAsync(u => u.Email == "newuser@example.com");
        user.IsEmailVerified.Should().BeFalse();
        user.EmailVerificationToken.Should().NotBeNullOrEmpty();
        user.EmailVerificationTokenExpiry.Should().BeAfter(DateTime.UtcNow);

        email.Verify(e => e.SendTemplateAsync(
                "newuser@example.com",
                "verify-email",
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyEmail_WithValidToken_MarksUserVerified()
    {
        var (service, db, _) = Build();
        var user = new User
        {
            Username = "u1",
            Email = "u1@example.com",
            PasswordHash = "h:p",
            EmailVerificationToken = "tok-abc",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(1),
            Role = UserRole.User,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await service.VerifyEmailAsync("tok-abc");

        result.Success.Should().BeTrue();
        var fresh = await db.Users.SingleAsync(u => u.Id == user.Id);
        fresh.IsEmailVerified.Should().BeTrue();
        fresh.EmailVerificationToken.Should().BeNull();
        fresh.EmailVerificationTokenExpiry.Should().BeNull();
    }

    [Fact]
    public async Task VerifyEmail_WithExpiredToken_Fails()
    {
        var (service, db, _) = Build();
        db.Users.Add(new User
        {
            Username = "u2",
            Email = "u2@example.com",
            PasswordHash = "h:p",
            EmailVerificationToken = "expired",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(-1),
            Role = UserRole.User,
        });
        await db.SaveChangesAsync();

        var result = await service.VerifyEmailAsync("expired");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("expired");
    }

    [Fact]
    public async Task ResendVerification_RegeneratesTokenAndSendsEmail()
    {
        var (service, db, email) = Build();
        var user = new User
        {
            Username = "u3",
            Email = "u3@example.com",
            PasswordHash = "h:p",
            EmailVerificationToken = "old-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddMinutes(5),
            Role = UserRole.User,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await service.ResendVerificationAsync(user.Id);

        result.Success.Should().BeTrue();
        var fresh = await db.Users.SingleAsync(u => u.Id == user.Id);
        fresh.EmailVerificationToken.Should().NotBe("old-token");
        fresh.EmailVerificationToken.Should().NotBeNullOrEmpty();

        email.Verify(e => e.SendTemplateAsync(
                "u3@example.com",
                "verify-email",
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
