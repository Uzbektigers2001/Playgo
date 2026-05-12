using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Playgo.Application.DTOs.Subscription;
using Playgo.Application.Services;
using Playgo.Domain.Entities;
using Playgo.Domain.Enums;
using Playgo.Infrastructure.Persistence;

namespace Playgo.Tests;

public class SubscriptionServiceTests
{
    private class FakePaymentProvider : IPaymentProviderService
    {
        public Task<string> CreatePaymentUrlAsync(Payment payment, CancellationToken cancellationToken = default)
            => Task.FromResult($"https://test/{payment.Id}");

        public Task<bool> VerifyCallbackAsync(string rawBody, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private static (SubscriptionService Service, PaymentService Payments, ApplicationDbContext Db, User User, Plan Plan, IServiceProvider Sp) Build()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"sub-{Guid.NewGuid()}")
            .Options;
        var db = new ApplicationDbContext(options);

        var user = new User { Username = "u", Email = "u@x.com", PasswordHash = "h", Role = UserRole.User };
        var plan = new Plan
        {
            Code = "premium_monthly",
            Name = "Premium",
            Price = 49000m,
            Currency = "UZS",
            Period = BillingPeriod.Monthly,
            IsActive = true,
            MaxQuality = 2160,
            MaxConcurrentStreams = 2,
            HasAds = false,
            AllowsDownload = true,
        };
        db.Users.Add(user);
        db.Plans.Add(plan);
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IPaymentProviderService, FakePaymentProvider>("Manual");
        services.AddKeyedSingleton<IPaymentProviderService, FakePaymentProvider>("Click");
        var sp = services.BuildServiceProvider();

        var paymentService = new PaymentService(db);
        var service = new SubscriptionService(db, paymentService, sp);
        return (service, paymentService, db, user, plan, sp);
    }

    [Fact]
    public async Task SubscribeAsync_CreatesPendingSubscriptionAndPayment()
    {
        var (service, _, db, user, plan, _) = Build();

        var result = await service.SubscribeAsync(user.Id, new SubscribeRequest(plan.Id, PaymentProvider.Manual));

        result.Success.Should().BeTrue();
        result.Data!.PaymentUrl.Should().StartWith("https://test/");

        var sub = await db.UserSubscriptions.AsNoTracking().SingleAsync();
        sub.Status.Should().Be(SubscriptionStatus.PendingPayment);

        var payment = await db.Payments.AsNoTracking().SingleAsync();
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.Amount.Should().Be(49000m);
        payment.SubscriptionId.Should().Be(sub.Id);
    }

    [Fact]
    public async Task CallbackMarkComplete_ActivatesSubscription()
    {
        var (service, payments, db, user, plan, _) = Build();
        var subResult = await service.SubscribeAsync(user.Id, new SubscribeRequest(plan.Id, PaymentProvider.Manual));
        var paymentId = subResult.Data!.PaymentId;

        var result = await payments.MarkCompletedAsync(paymentId, "tx-123", "{}");

        result.Success.Should().BeTrue();
        var sub = await db.UserSubscriptions.AsNoTracking().SingleAsync();
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddDays(27));

        var p = await db.Payments.AsNoTracking().SingleAsync();
        p.Status.Should().Be(PaymentStatus.Completed);
        p.ProviderTransactionId.Should().Be("tx-123");
    }

    [Fact]
    public async Task CancelAsync_DoesNotChangeExpiresAt()
    {
        var (service, payments, db, user, plan, _) = Build();
        var subResult = await service.SubscribeAsync(user.Id, new SubscribeRequest(plan.Id, PaymentProvider.Manual));
        await payments.MarkCompletedAsync(subResult.Data!.PaymentId, "tx", null);

        var beforeExpiry = (await db.UserSubscriptions.AsNoTracking().SingleAsync()).ExpiresAt;

        var cancel = await service.CancelAsync(user.Id);

        cancel.Success.Should().BeTrue();
        var sub = await db.UserSubscriptions.AsNoTracking().SingleAsync();
        sub.Status.Should().Be(SubscriptionStatus.Cancelled);
        sub.CancelledAt.Should().NotBeNull();
        sub.ExpiresAt.Should().Be(beforeExpiry);
    }

    [Fact]
    public async Task IsActiveAsync_ChecksStatusAndExpiry()
    {
        var (service, _, db, user, plan, _) = Build();

        (await service.IsActiveAsync(user.Id)).Should().BeFalse();

        db.UserSubscriptions.Add(new UserSubscription
        {
            UserId = user.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            StartedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        });
        await db.SaveChangesAsync();
        (await service.IsActiveAsync(user.Id)).Should().BeTrue();

        var sub = await db.UserSubscriptions.SingleAsync();
        sub.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();
        (await service.IsActiveAsync(user.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task SubscribeAsync_AlreadyActive_Prevented()
    {
        var (service, _, db, user, plan, _) = Build();
        db.UserSubscriptions.Add(new UserSubscription
        {
            UserId = user.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            StartedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(15),
        });
        await db.SaveChangesAsync();

        var result = await service.SubscribeAsync(user.Id, new SubscribeRequest(plan.Id, PaymentProvider.Manual));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("already");
    }
}
