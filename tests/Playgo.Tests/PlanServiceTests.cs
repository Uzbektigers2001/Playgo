using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Subscription;
using Playgo.Application.Services;
using Playgo.Domain.Entities;
using Playgo.Infrastructure.Persistence;

namespace Playgo.Tests;

public class PlanServiceTests
{
    private static (PlanService Service, ApplicationDbContext Db) Build()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"plan-{Guid.NewGuid()}")
            .Options;
        var db = new ApplicationDbContext(options);

        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetAsync<List<PlanDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<PlanDto>?)null);

        var service = new PlanService(db, cache.Object);
        return (service, db);
    }

    [Fact]
    public async Task CreateAsync_AddsPlanAndReturnsDto()
    {
        var (service, db) = Build();
        var req = new CreatePlanRequest(
            "premium_monthly", "Premium Monthly", "Best plan", 49000m, "UZS",
            BillingPeriod.Monthly, 2160, 2, false, true,
            new List<string> { "4K", "No ads" });

        var result = await service.CreateAsync(req);

        result.Success.Should().BeTrue();
        result.Data!.Code.Should().Be("premium_monthly");
        result.Data.Period.Should().Be("Monthly");
        result.Data.Features.Should().Contain("4K");

        var row = await db.Plans.AsNoTracking().SingleAsync();
        row.Price.Should().Be(49000m);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_Fails()
    {
        var (service, _) = Build();
        var req = new CreatePlanRequest("free", "Free", null, 0m, "UZS", BillingPeriod.Monthly, 720, 1, true, false);
        (await service.CreateAsync(req)).Success.Should().BeTrue();
        var second = await service.CreateAsync(req);
        second.Success.Should().BeFalse();
        second.Error.Should().Contain("already");
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActive_SortedByPrice()
    {
        var (service, db) = Build();
        db.Plans.AddRange(
            new Plan { Code = "p1", Name = "P1", Price = 100m, Period = BillingPeriod.Monthly, IsActive = true, MaxQuality = 720 },
            new Plan { Code = "p2", Name = "P2", Price = 50m, Period = BillingPeriod.Monthly, IsActive = true, MaxQuality = 720 },
            new Plan { Code = "p3", Name = "P3", Price = 10m, Period = BillingPeriod.Monthly, IsActive = false, MaxQuality = 720 });
        await db.SaveChangesAsync();

        var list = await service.GetActiveAsync();

        list.Should().HaveCount(2);
        list[0].Code.Should().Be("p2");
        list[1].Code.Should().Be("p1");
    }
}
