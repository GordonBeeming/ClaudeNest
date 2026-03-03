using ClaudeNest.Shared.Enums;

namespace ClaudeNest.Backend.Data.Entities;

public class Account
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Guid? PlanId { get; set; }
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.None;
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? StripePaymentMethodFingerprint { get; set; }
    public DateTimeOffset? CurrentPeriodEnd { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public string PermissionMode { get; set; } = "default";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Plan? Plan { get; set; }
    public ICollection<User> Users { get; set; } = [];
    public ICollection<Agent> Agents { get; set; } = [];
    public ICollection<CouponRedemption> CouponRedemptions { get; set; } = [];
}
