using Microsoft.EntityFrameworkCore;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Subscription;
using Playgo.Domain.Entities;

namespace Playgo.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IApplicationDbContext _db;

    public PaymentService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<InitiatePaymentResult>> InitiateAsync(Guid userId, Guid planId, Guid? subscriptionId, PaymentProvider provider, CancellationToken cancellationToken = default)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == planId && !p.IsDeleted, cancellationToken);
        if (plan is null)
            return Result<InitiatePaymentResult>.Fail("Plan not found.");

        var payment = new Payment
        {
            UserId = userId,
            SubscriptionId = subscriptionId,
            PlanId = plan.Id,
            Amount = plan.Price,
            Currency = plan.Currency,
            Provider = provider,
            Status = PaymentStatus.Pending,
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        var url = $"https://demo-payment/{provider.ToString().ToLowerInvariant()}?paymentId={payment.Id}";
        return Result<InitiatePaymentResult>.Ok(new InitiatePaymentResult(payment.Id, url));
    }

    public async Task<Result> MarkCompletedAsync(Guid paymentId, string? providerTransactionId, string? rawResponse, CancellationToken cancellationToken = default)
    {
        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.Id == paymentId && !p.IsDeleted, cancellationToken);

        if (payment is null)
            return Result.Fail("Payment not found.");

        if (payment.Status == PaymentStatus.Completed)
            return Result.Ok();

        var now = DateTime.UtcNow;
        payment.Status = PaymentStatus.Completed;
        payment.CompletedAt = now;
        payment.ProviderTransactionId = providerTransactionId;
        payment.RawResponseJson = rawResponse;
        payment.UpdatedAt = now;

        if (payment.SubscriptionId.HasValue)
        {
            var subscription = await _db.UserSubscriptions
                .FirstOrDefaultAsync(s => s.Id == payment.SubscriptionId.Value && !s.IsDeleted, cancellationToken);

            if (subscription is not null)
            {
                var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == subscription.PlanId, cancellationToken);
                subscription.Status = SubscriptionStatus.Active;
                subscription.StartedAt = now;
                subscription.ExpiresAt = plan is not null
                    ? SubscriptionService.ComputeExpiry(now, plan.Period)
                    : subscription.ExpiresAt;
                subscription.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<PagedResult<PaymentDto>> GetMineAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : pageSize;

        var query = _db.Payments.AsNoTracking().Where(p => !p.IsDeleted && p.UserId == userId);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PaymentDto(
                p.Id,
                p.Amount,
                p.Currency,
                p.Provider.ToString(),
                p.Status.ToString(),
                p.CreatedAt,
                p.CompletedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<PaymentDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<PagedResult<PaymentDto>> AdminGetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : pageSize;

        var query = _db.Payments.AsNoTracking().Where(p => !p.IsDeleted);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PaymentDto(
                p.Id,
                p.Amount,
                p.Currency,
                p.Provider.ToString(),
                p.Status.ToString(),
                p.CreatedAt,
                p.CompletedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<PaymentDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }
}
