namespace ClaudeNest.Backend.Data.Entities;

public class CouponRedemption
{
    public Guid Id { get; set; }
    public Guid CouponId { get; set; }
    public Guid AccountId { get; set; }
    public DateTimeOffset RedeemedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset FreeUntil { get; set; }
    public string? StripeCheckoutSessionId { get; set; }

    public Coupon Coupon { get; set; } = null!;
    public Account Account { get; set; } = null!;
}
