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
}
