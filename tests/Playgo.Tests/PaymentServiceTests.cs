using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Playgo.Application.Services;
using Playgo.Domain.Entities;
using Playgo.Domain.Enums;
using Playgo.Infrastructure.Persistence;

namespace Playgo.Tests;

public class PaymentServiceTests
{
    private static (PaymentService Service, ApplicationDbContext Db, User User, Plan Plan) Build()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"pay-{Guid.NewGuid()}")
            .Options;
        var db = new ApplicationDbContext(options);

        var user = new User { Username = "u", Email = "u@x.com", PasswordHash = "h", Role = UserRole.User };
        var plan = new Plan
        {
            Code = "free", Name = "Free", Price = 100m, Currency = "UZS",
            Period = BillingPeriod.Monthly, IsActive = true, MaxQuality = 720,
        };
        db.Users.Add(user);
        db.Plans.Add(plan);
        db.SaveChanges();

        return (new PaymentService(db), db, user, plan);
    }

    [Fact]
    public async Task InitiateAsync_CreatesPendingPayment()
    {
        var (service, db, user, plan) = Build();

        var result = await service.InitiateAsync(user.Id, plan.Id, null, PaymentProvider.Click);

        result.Success.Should().BeTrue();
        result.Data!.PaymentUrl.Should().Contain("click");

        var payment = await db.Payments.AsNoTracking().SingleAsync();
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.Amount.Should().Be(100m);
        payment.Provider.Should().Be(PaymentProvider.Click);
    }

    [Fact]
    public async Task MarkCompletedAsync_IdempotentOnSecondCall()
    {
        var (service, db, user, plan) = Build();
        var init = await service.InitiateAsync(user.Id, plan.Id, null, PaymentProvider.Manual);

        (await service.MarkCompletedAsync(init.Data!.PaymentId, "tx-1", null)).Success.Should().BeTrue();
        var firstCompletedAt = (await db.Payments.AsNoTracking().SingleAsync()).CompletedAt;

        (await service.MarkCompletedAsync(init.Data.PaymentId, "tx-2", null)).Success.Should().BeTrue();
        var payment = await db.Payments.AsNoTracking().SingleAsync();
        payment.Status.Should().Be(PaymentStatus.Completed);
        payment.CompletedAt.Should().Be(firstCompletedAt);
        payment.ProviderTransactionId.Should().Be("tx-1");
    }

    [Fact]
    public async Task GetMineAsync_ReturnsPaymentsForUserOnly()
    {
        var (service, db, user, plan) = Build();
        var other = new User { Username = "o", Email = "o@x.com", PasswordHash = "h" };
        db.Users.Add(other);
        await db.SaveChangesAsync();

        await service.InitiateAsync(user.Id, plan.Id, null, PaymentProvider.Click);
        await service.InitiateAsync(user.Id, plan.Id, null, PaymentProvider.Manual);
        await service.InitiateAsync(other.Id, plan.Id, null, PaymentProvider.Payme);

        var mine = await service.GetMineAsync(user.Id, 1, 20);
        mine.TotalCount.Should().Be(2);
        mine.Items.Should().OnlyContain(p => p.Provider == "Click" || p.Provider == "Manual");
    }
}
