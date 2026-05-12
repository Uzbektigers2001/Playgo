using Playgo.Domain.Common;

namespace Playgo.Domain.Entities;

public enum SubscriptionStatus
{
    Active = 1,
    Cancelled = 2,
    Expired = 3,
    PendingPayment = 4
}

public class UserSubscription : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid PlanId { get; set; }
    public Plan? Plan { get; set; }

    public SubscriptionStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public bool AutoRenew { get; set; } = false;
}
