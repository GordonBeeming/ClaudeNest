using System.ComponentModel.DataAnnotations;
using ClaudeNest.Shared.Enums;

namespace ClaudeNest.Backend.Models;

public record CreateCouponRequest(
    [Required] string Code,
    Guid PlanId,
    int FreeMonths,
    int MaxRedemptions,
    DateTimeOffset? ExpiresAt,
    DiscountType DiscountType = DiscountType.FreeMonths,
    decimal? PercentOff = null,
    int? AmountOffCents = null,
    int? FreeDays = null,
    int? DurationMonths = null);
