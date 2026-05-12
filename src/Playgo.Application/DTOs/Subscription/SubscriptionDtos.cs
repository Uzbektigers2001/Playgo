using Playgo.Domain.Entities;

namespace Playgo.Application.DTOs.Subscription;

public record PlanDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    string Period,
    int MaxQuality,
    int MaxConcurrentStreams,
    bool HasAds,
    bool AllowsDownload,
    List<string> Features);

public record CreatePlanRequest(
    string Code,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    BillingPeriod Period,
    int MaxQuality,
    int MaxConcurrentStreams,
    bool HasAds,
    bool AllowsDownload,
    List<string>? Features = null);

public record UpdatePlanRequest(
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    BillingPeriod Period,
    int MaxQuality,
    int MaxConcurrentStreams,
    bool HasAds,
    bool AllowsDownload,
    bool IsActive,
    List<string>? Features = null);

public record UserSubscriptionDto(
    Guid Id,
    PlanDto Plan,
    string Status,
    DateTime StartedAt,
    DateTime ExpiresAt,
    bool AutoRenew,
    int DaysRemaining);

public record SubscribeRequest(
    Guid PlanId,
    PaymentProvider Provider);

public record PaymentDto(
    Guid Id,
    decimal Amount,
    string Currency,
    string Provider,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public record InitiatePaymentResult(
    Guid PaymentId,
    string PaymentUrl);
