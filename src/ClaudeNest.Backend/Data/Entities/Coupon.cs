using ClaudeNest.Shared.Enums;

namespace ClaudeNest.Backend.Data.Entities;

public class Coupon
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public Guid PlanId { get; set; }
    public int FreeMonths { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal? PercentOff { get; set; }
    public int? AmountOffCents { get; set; }
    public int? FreeDays { get; set; }
    public int DurationMonths { get; set; }
    public int MaxRedemptions { get; set; }
    public int TimesRedeemed { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? StripeCouponId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid CreatedByUserId { get; set; }

    public Plan Plan { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<CouponRedemption> Redemptions { get; set; } = [];
}
