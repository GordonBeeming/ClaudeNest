using ClaudeNest.Backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlansController(NestDbContext db, TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPlans()
    {
        var now = timeProvider.GetUtcNow();
        var plans = await db.Plans
            .Include(p => p.DefaultCoupon)
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.MaxAgents,
                p.MaxSessions,
                p.PriceCents,
                p.SortOrder,
                DefaultCoupon = p.DefaultCoupon != null
                    && p.DefaultCoupon.IsActive
                    && p.DefaultCoupon.TimesRedeemed < p.DefaultCoupon.MaxRedemptions
                    && (p.DefaultCoupon.ExpiresAt == null || p.DefaultCoupon.ExpiresAt > now)
                    ? new
                    {
                        p.DefaultCoupon.FreeMonths,
                        DiscountType = p.DefaultCoupon.DiscountType.ToString(),
                        p.DefaultCoupon.PercentOff,
                        p.DefaultCoupon.AmountOffCents,
                        p.DefaultCoupon.FreeDays,
                        p.DefaultCoupon.DurationMonths
                    }
                    : null
            })
            .ToListAsync();

        return Ok(plans);
    }

    [HttpGet("coupon/{code}")]
    public async Task<IActionResult> GetCouponByCode(string code)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var now = timeProvider.GetUtcNow();

        var coupon = await db.Coupons
            .Include(c => c.Plan)
            .FirstOrDefaultAsync(c => c.Code == normalizedCode);

        if (coupon is null)
            return Ok(new { Valid = false, Reason = "Coupon not found" });

        if (!coupon.IsActive)
            return Ok(new { Valid = false, Reason = "Coupon is no longer active" });

        if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt.Value < now)
            return Ok(new { Valid = false, Reason = "Coupon has expired" });

        if (coupon.TimesRedeemed >= coupon.MaxRedemptions)
            return Ok(new { Valid = false, Reason = "Coupon has reached maximum redemptions" });

        return Ok(new
        {
            Valid = true,
            CouponId = coupon.Id,
            coupon.Code,
            PlanId = coupon.PlanId,
            PlanName = coupon.Plan.Name,
            PlanPriceCents = coupon.Plan.PriceCents,
            PlanMaxAgents = coupon.Plan.MaxAgents,
            PlanMaxSessions = coupon.Plan.MaxSessions,
            PlanSortOrder = coupon.Plan.SortOrder,
            coupon.FreeMonths,
            DiscountType = coupon.DiscountType.ToString(),
            coupon.PercentOff,
            coupon.AmountOffCents,
            coupon.FreeDays,
            coupon.DurationMonths,
            coupon.ExpiresAt
        });
    }
}
