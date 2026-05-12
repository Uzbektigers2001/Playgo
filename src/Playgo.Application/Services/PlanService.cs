using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Playgo.Application.Common;
using Playgo.Application.Common.Interfaces;
using Playgo.Application.DTOs.Subscription;
using Playgo.Domain.Entities;

namespace Playgo.Application.Services;

public class PlanService : IPlanService
{
    private const string ActivePlansCacheKey = "plans:active";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;

    public PlanService(IApplicationDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<List<PlanDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetAsync<List<PlanDto>>(ActivePlansCacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var plans = await _db.Plans
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsActive)
            .OrderBy(p => p.Price)
            .ToListAsync(cancellationToken);

        var list = plans.Select(MapToDto).ToList();
        await _cache.SetAsync(ActivePlansCacheKey, list, CacheTtl, cancellationToken);
        return list;
    }

    public async Task<Result<PlanDto>> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var plan = await _db.Plans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => !p.IsDeleted && p.Code == code, cancellationToken);

        if (plan is null)
            return Result<PlanDto>.Fail("Plan not found.");

        return Result<PlanDto>.Ok(MapToDto(plan));
    }

    public async Task<Result<PlanDto>> CreateAsync(CreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        if (await _db.Plans.AnyAsync(p => p.Code == request.Code && !p.IsDeleted, cancellationToken))
            return Result<PlanDto>.Fail("Plan code already exists.");

        var plan = new Plan
        {
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "UZS" : request.Currency,
            Period = request.Period,
            MaxQuality = request.MaxQuality,
            MaxConcurrentStreams = request.MaxConcurrentStreams,
            HasAds = request.HasAds,
            AllowsDownload = request.AllowsDownload,
            FeaturesJson = request.Features is { Count: > 0 } ? JsonSerializer.Serialize(request.Features) : null,
            IsActive = true,
        };

        _db.Plans.Add(plan);
        await _db.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(ActivePlansCacheKey, cancellationToken);

        return Result<PlanDto>.Ok(MapToDto(plan));
    }

    public async Task<Result<PlanDto>> UpdateAsync(Guid id, UpdatePlanRequest request, CancellationToken cancellationToken = default)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (plan is null)
            return Result<PlanDto>.Fail("Plan not found.");

        plan.Name = request.Name;
        plan.Description = request.Description;
        plan.Price = request.Price;
        plan.Currency = string.IsNullOrWhiteSpace(request.Currency) ? plan.Currency : request.Currency;
        plan.Period = request.Period;
        plan.MaxQuality = request.MaxQuality;
        plan.MaxConcurrentStreams = request.MaxConcurrentStreams;
        plan.HasAds = request.HasAds;
        plan.AllowsDownload = request.AllowsDownload;
        plan.IsActive = request.IsActive;
        plan.FeaturesJson = request.Features is { Count: > 0 } ? JsonSerializer.Serialize(request.Features) : null;
        plan.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(ActivePlansCacheKey, cancellationToken);

        return Result<PlanDto>.Ok(MapToDto(plan));
    }

    public async Task<Result> DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (plan is null)
            return Result.Fail("Plan not found.");

        plan.IsActive = false;
        plan.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(ActivePlansCacheKey, cancellationToken);

        return Result.Ok();
    }

    internal static PlanDto MapToDto(Plan p)
    {
        var features = new List<string>();
        if (!string.IsNullOrWhiteSpace(p.FeaturesJson))
        {
            try
            {
                features = JsonSerializer.Deserialize<List<string>>(p.FeaturesJson) ?? new List<string>();
            }
            catch
            {
                features = new List<string>();
            }
        }

        return new PlanDto(
            p.Id,
            p.Code,
            p.Name,
            p.Description,
            p.Price,
            p.Currency,
            p.Period.ToString(),
            p.MaxQuality,
            p.MaxConcurrentStreams,
            p.HasAds,
            p.AllowsDownload,
            features);
    }
}
