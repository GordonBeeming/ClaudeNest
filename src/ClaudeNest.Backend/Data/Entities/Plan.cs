namespace ClaudeNest.Backend.Data.Entities;

public class Plan
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int MaxAgents { get; set; }
    public int MaxSessions { get; set; }
    public int PriceCents { get; set; }
    public string? StripeProductId { get; set; }
    public string? StripePriceId { get; set; }
    public Guid? DefaultCouponId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public Coupon? DefaultCoupon { get; set; }
}
