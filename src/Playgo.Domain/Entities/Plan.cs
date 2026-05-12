using Playgo.Domain.Common;

namespace Playgo.Domain.Entities;

public enum BillingPeriod
{
    Monthly = 1,
    Quarterly = 2,
    Yearly = 3,
    Lifetime = 99
}

public class Plan : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "UZS";
    public BillingPeriod Period { get; set; }
    public bool IsActive { get; set; } = true;
    public string? FeaturesJson { get; set; }
    public int MaxQuality { get; set; } = 720;
    public int MaxConcurrentStreams { get; set; } = 1;
    public bool HasAds { get; set; } = true;
    public bool AllowsDownload { get; set; } = false;

    public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
}
