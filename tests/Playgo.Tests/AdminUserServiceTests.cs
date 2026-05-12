using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Playgo.Application.DTOs.Admin;
using Playgo.Application.Services;
using Playgo.Domain.Entities;
using Playgo.Domain.Enums;
using Playgo.Infrastructure.Persistence;

namespace Playgo.Tests;

public class AdminUserServiceTests
{
    private static (AdminUserService Service, ApplicationDbContext Db) Build()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"admin-users-{Guid.NewGuid()}")
            .Options;
        var db = new ApplicationDbContext(options);
        return (new AdminUserService(db, NullLogger<AdminUserService>.Instance), db);
    }

    private static User CreateUser(string username, string email, UserRole role = UserRole.User, bool banned = false)
        => new()
        {
            Username = username,
            Email = email,
            PasswordHash = "h",
            Role = role,
            IsBanned = banned,
        };

    [Fact]
    public async Task Search_MatchesUsernameEmailAndFullName()
    {
        var (service, db) = Build();
        db.Users.AddRange(
            new User { Username = "alice", Email = "alice@x.com", PasswordHash = "h", FullName = "Alice Wonder" },
            new User { Username = "bob", Email = "bob@x.com", PasswordHash = "h", FullName = "Bob Marley" },
            new User { Username = "charlie", Email = "ch@y.com", PasswordHash = "h", FullName = "Wonderful Charlie" });
        await db.SaveChangesAsync();

        var result = await service.GetUsersAsync(new AdminUserFilterRequest { Search = "wonder" });

        result.TotalCount.Should().Be(2);
        result.Items.Select(i => i.Username).Should().BeEquivalentTo(new[] { "alice", "charlie" });
    }

    [Fact]
    public async Task Filter_ByRoleAndBanned_Works()
    {
        var (service, db) = Build();
        db.Users.AddRange(
            CreateUser("u1", "u1@x.com", UserRole.User, banned: false),
            CreateUser("m1", "m1@x.com", UserRole.Moderator, banned: false),
            CreateUser("u2", "u2@x.com", UserRole.User, banned: true));
        await db.SaveChangesAsync();

        var mods = await service.GetUsersAsync(new AdminUserFilterRequest { Role = UserRole.Moderator });
        mods.TotalCount.Should().Be(1);
        mods.Items.Single().Username.Should().Be("m1");

        var banned = await service.GetUsersAsync(new AdminUserFilterRequest { IsBanned = true });
        banned.TotalCount.Should().Be(1);
        banned.Items.Single().Username.Should().Be("u2");
    }

    [Fact]
    public async Task Ban_SetsFieldsAndClearsRefreshToken()
    {
        var (service, db) = Build();
        var admin = CreateUser("admin", "a@x.com", UserRole.Admin);
        var target = CreateUser("target", "t@x.com");
        target.RefreshToken = "stale-token";
        db.Users.AddRange(admin, target);
        await db.SaveChangesAsync();

        var result = await service.BanAsync(target.Id, "spam", admin.Id);

        result.Success.Should().BeTrue();
        var stored = await db.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
        stored.IsBanned.Should().BeTrue();
        stored.BanReason.Should().Be("spam");
        stored.BannedAt.Should().NotBeNull();
        stored.RefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task Unban_ResetsFields()
    {
        var (service, db) = Build();
        var target = CreateUser("u", "u@x.com", banned: true);
        target.BanReason = "spam";
        target.BannedAt = DateTime.UtcNow;
        db.Users.Add(target);
        await db.SaveChangesAsync();

        var result = await service.UnbanAsync(target.Id);

        result.Success.Should().BeTrue();
        var stored = await db.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
        stored.IsBanned.Should().BeFalse();
        stored.BanReason.Should().BeNull();
        stored.BannedAt.Should().BeNull();
    }

    [Fact]
    public async Task Ban_CannotBanSelf()
    {
        var (service, db) = Build();
        var admin = CreateUser("admin", "a@x.com", UserRole.Admin);
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var result = await service.BanAsync(admin.Id, "test", admin.Id);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("yourself");
        var stored = await db.Users.AsNoTracking().SingleAsync(u => u.Id == admin.Id);
        stored.IsBanned.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_CannotDeleteSelf()
    {
        var (service, db) = Build();
        var admin = CreateUser("admin", "a@x.com", UserRole.Admin);
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var result = await service.DeleteAsync(admin.Id, admin.Id);

        result.Success.Should().BeFalse();
        var stored = await db.Users.IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.Id == admin.Id);
        stored.IsDeleted.Should().BeFalse();
    }
}
