using Playgo.Domain.Common;

namespace Playgo.Domain.Entities;

public enum PaymentStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3,
    Refunded = 4
}

public enum PaymentProvider
{
    Click = 1,
    Payme = 2,
    Stripe = 3,
    Manual = 99
}

public class Payment : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid? SubscriptionId { get; set; }
    public UserSubscription? Subscription { get; set; }

    public Guid? PlanId { get; set; }
    public Plan? Plan { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "UZS";
    public PaymentProvider Provider { get; set; }
    public PaymentStatus Status { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? RawResponseJson { get; set; }
    public DateTime? CompletedAt { get; set; }
}
