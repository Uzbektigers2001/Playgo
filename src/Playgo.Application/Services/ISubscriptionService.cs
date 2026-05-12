using Playgo.Application.Common;
using Playgo.Application.DTOs.Subscription;

namespace Playgo.Application.Services;

public interface ISubscriptionService
{
    Task<Result<InitiatePaymentResult>> SubscribeAsync(Guid userId, SubscribeRequest request, CancellationToken cancellationToken = default);
    Task<Result> CancelAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<UserSubscriptionDto>> GetMineAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<PlanDto>> GetCurrentPlanAsync(Guid userId, CancellationToken cancellationToken = default);
}
