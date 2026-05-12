using Playgo.Application.Common;
using Playgo.Application.DTOs.Subscription;

namespace Playgo.Application.Services;

public interface IPlanService
{
    Task<List<PlanDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<Result<PlanDto>> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<Result<PlanDto>> CreateAsync(CreatePlanRequest request, CancellationToken cancellationToken = default);
    Task<Result<PlanDto>> UpdateAsync(Guid id, UpdatePlanRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
}
