using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Subscription;
using Playgo.Domain.Entities;

namespace Playgo.Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentService _paymentService;
    private readonly IServiceProvider _serviceProvider;

    public SubscriptionService(IApplicationDbContext db, IPaymentService paymentService, IServiceProvider serviceProvider)
    {
        _db = db;
        _paymentService = paymentService;
        _serviceProvider = serviceProvider;
    }

    public async Task<Result<InitiatePaymentResult>> SubscribeAsync(Guid userId, SubscribeRequest request, CancellationToken cancellationToken = default)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == request.PlanId && !p.IsDeleted && p.IsActive, cancellationToken);
        if (plan is null)
            return Result<InitiatePaymentResult>.Fail("Plan not found or inactive.");

        var existingActive = await _db.UserSubscriptions
            .AnyAsync(s => s.UserId == userId
                && !s.IsDeleted
                && s.Status == SubscriptionStatus.Active
                && s.ExpiresAt > DateTime.UtcNow, cancellationToken);

        if (existingActive)
            return Result<InitiatePaymentResult>.Fail("You already have an active subscription.");

        var now = DateTime.UtcNow;
        var subscription = new UserSubscription
        {
            UserId = userId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.PendingPayment,
            StartedAt = now,
            ExpiresAt = ComputeExpiry(now, plan.Period),
            AutoRenew = false,
        };

        _db.UserSubscriptions.Add(subscription);

        var payment = new Payment
        {
            UserId = userId,
            SubscriptionId = subscription.Id,
            PlanId = plan.Id,
            Amount = plan.Price,
            Currency = plan.Currency,
            Provider = request.Provider,
            Status = PaymentStatus.Pending,
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        var provider = _serviceProvider.GetRequiredKeyedService<IPaymentProviderService>(request.Provider.ToString());
        var url = await provider.CreatePaymentUrlAsync(payment, cancellationToken);

        return Result<InitiatePaymentResult>.Ok(new InitiatePaymentResult(payment.Id, url));
    }

    public async Task<Result> CancelAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var subscription = await _db.UserSubscriptions
            .Where(s => s.UserId == userId && !s.IsDeleted && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
            return Result.Fail("No active subscription to cancel.");

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.CancelledAt = DateTime.UtcNow;
        subscription.AutoRenew = false;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<UserSubscriptionDto>> GetMineAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var subscription = await _db.UserSubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null || subscription.Plan is null)
            return Result<UserSubscriptionDto>.Fail("No subscription found.");

        var days = (int)Math.Max(0, (subscription.ExpiresAt - DateTime.UtcNow).TotalDays);
        var dto = new UserSubscriptionDto(
            subscription.Id,
            PlanService.MapToDto(subscription.Plan),
            subscription.Status.ToString(),
            subscription.StartedAt,
            subscription.ExpiresAt,
            subscription.AutoRenew,
            days);

        return Result<UserSubscriptionDto>.Ok(dto);
    }

    public Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _db.UserSubscriptions
            .AnyAsync(s => s.UserId == userId
                && !s.IsDeleted
                && s.Status == SubscriptionStatus.Active
                && s.ExpiresAt > DateTime.UtcNow, cancellationToken);
    }

    public async Task<Result<PlanDto>> GetCurrentPlanAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var subscription = await _db.UserSubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => s.UserId == userId
                && !s.IsDeleted
                && s.Status == SubscriptionStatus.Active
                && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription?.Plan is not null)
            return Result<PlanDto>.Ok(PlanService.MapToDto(subscription.Plan));

        var free = await _db.Plans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => !p.IsDeleted && p.IsActive && p.Code == "free", cancellationToken);

        if (free is null)
            return Result<PlanDto>.Fail("No active plan found.");

        return Result<PlanDto>.Ok(PlanService.MapToDto(free));
    }

    internal static DateTime ComputeExpiry(DateTime from, BillingPeriod period) => period switch
    {
        BillingPeriod.Monthly => from.AddMonths(1),
        BillingPeriod.Quarterly => from.AddMonths(3),
        BillingPeriod.Yearly => from.AddYears(1),
        BillingPeriod.Lifetime => from.AddYears(100),
        _ => from.AddMonths(1),
    };
}
