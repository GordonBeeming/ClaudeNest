using ClaudeNest.Backend.Auth;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Data.Entities;
using ClaudeNest.Backend.Models;
using ClaudeNest.Backend.Stripe;
using ClaudeNest.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/admin/coupons")]
[Authorize]
[AdminRequired]
public class AdminCouponsController(NestDbContext db, IStripeService stripeService, ILogger<AdminCouponsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListCoupons()
    {
        var coupons = await db.Coupons
            .Include(c => c.Plan)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Code,
                c.PlanId,
                PlanName = c.Plan.Name,
                c.FreeMonths,
                DiscountType = c.DiscountType.ToString(),
                c.PercentOff,
                c.AmountOffCents,
                c.FreeDays,
                c.DurationMonths,
                c.MaxRedemptions,
                c.TimesRedeemed,
                c.ExpiresAt,
                c.StripeCouponId,
                c.IsActive,
                c.CreatedAt,
                IsDefaultForPlan = c.Plan.DefaultCouponId == c.Id
            })
            .ToListAsync();

        return Ok(coupons);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCoupon([FromBody] CreateCouponRequest request)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Auth0UserId == auth0UserId);
        if (user is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest("Code is required");

        if (request.MaxRedemptions < 1)
            return BadRequest("Max redemptions must be at least 1");

        // Validate discount type fields
        switch (request.DiscountType)
        {
            case DiscountType.FreeMonths:
                if (request.FreeMonths < 1)
                    return BadRequest("Free months must be at least 1");
                break;
            case DiscountType.PercentOff:
                if (request.PercentOff is null or < 1 or > 100)
                    return BadRequest("Percent off must be between 1 and 100");
                if (request.DurationMonths is null or < 1)
                    return BadRequest("Duration months must be at least 1 for PercentOff");
                break;
            case DiscountType.AmountOff:
                if (request.AmountOffCents is null or < 1)
                    return BadRequest("Amount off must be at least 1 cent");
                if (request.DurationMonths is null or < 1)
                    return BadRequest("Duration months must be at least 1 for AmountOff");
                break;
            case DiscountType.FreeDays:
                if (request.FreeDays is null or < 1)
                    return BadRequest("Free days must be at least 1");
                break;
            default:
                return BadRequest("Invalid discount type");
        }

        var plan = await db.Plans.FindAsync(request.PlanId);
        if (plan is null) return BadRequest("Invalid plan");

        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        if (await db.Coupons.AnyAsync(c => c.Code == normalizedCode && c.IsActive))
            return BadRequest("Coupon code already exists");

        string? stripeCouponId = null;
        try
        {
            stripeCouponId = await stripeService.CreateStripeCouponAsync(
                normalizedCode,
                request.DiscountType,
                request.FreeMonths,
                request.PercentOff,
                request.AmountOffCents,
                request.FreeDays,
                request.DurationMonths);
        }
        catch
        {
            // Stripe may not be configured in dev; continue without Stripe coupon
        }

        var coupon = new Coupon
        {
            Code = normalizedCode,
            PlanId = request.PlanId,
            FreeMonths = request.FreeMonths,
            DiscountType = request.DiscountType,
            PercentOff = request.PercentOff,
            AmountOffCents = request.AmountOffCents,
            FreeDays = request.FreeDays,
            DurationMonths = request.DurationMonths ?? 0,
            MaxRedemptions = request.MaxRedemptions,
            ExpiresAt = request.ExpiresAt,
            StripeCouponId = stripeCouponId,
            CreatedByUserId = user.Id
        };

        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();

        logger.LogInformation("Admin created coupon {Code} for plan {PlanId}", normalizedCode, request.PlanId);

        return Ok(new
        {
            coupon.Id,
            coupon.Code,
            coupon.PlanId,
            PlanName = plan.Name,
            coupon.FreeMonths,
            DiscountType = coupon.DiscountType.ToString(),
            coupon.PercentOff,
            coupon.AmountOffCents,
            coupon.FreeDays,
            coupon.DurationMonths,
            coupon.MaxRedemptions,
            coupon.TimesRedeemed,
            coupon.ExpiresAt,
            coupon.StripeCouponId,
            coupon.IsActive,
            coupon.CreatedAt
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCoupon(Guid id, [FromBody] UpdateCouponRequest request)
    {
        var coupon = await db.Coupons.FindAsync(id);
        if (coupon is null) return NotFound();

        if (request.MaxRedemptions.HasValue)
            coupon.MaxRedemptions = request.MaxRedemptions.Value;
        if (request.ExpiresAt.HasValue)
            coupon.ExpiresAt = request.ExpiresAt.Value;
        if (request.IsActive.HasValue)
            coupon.IsActive = request.IsActive.Value;

        await db.SaveChangesAsync();

        logger.LogInformation("Admin updated coupon {CouponId}", id);

        return Ok(new
        {
            coupon.Id,
            coupon.Code,
            coupon.PlanId,
            coupon.FreeMonths,
            DiscountType = coupon.DiscountType.ToString(),
            coupon.PercentOff,
            coupon.AmountOffCents,
            coupon.FreeDays,
            coupon.DurationMonths,
            coupon.MaxRedemptions,
            coupon.TimesRedeemed,
            coupon.ExpiresAt,
            coupon.StripeCouponId,
            coupon.IsActive,
            coupon.CreatedAt
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeactivateCoupon(Guid id)
    {
        var coupon = await db.Coupons.FindAsync(id);
        if (coupon is null) return NotFound();

        coupon.IsActive = false;

        if (!string.IsNullOrEmpty(coupon.StripeCouponId))
        {
            try
            {
                await stripeService.DeactivateStripeCouponAsync(coupon.StripeCouponId);
            }
            catch
            {
                // Continue even if Stripe deactivation fails
            }
        }

        await db.SaveChangesAsync();

        logger.LogInformation("Admin deactivated coupon {CouponId}", id);

        return Ok(new { coupon.Id, coupon.IsActive });
    }

    [HttpPost("{id:guid}/set-default")]
    public async Task<IActionResult> SetDefaultCoupon(Guid id, [FromBody] SetDefaultCouponRequest request)
    {
        var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Id == id);
        if (coupon is null) return NotFound();

        var plan = await db.Plans.AsTracking().FirstOrDefaultAsync(p => p.Id == coupon.PlanId);
        if (plan is null) return BadRequest("Plan not found");

        if (request.IsDefault)
        {
            plan.DefaultCouponId = coupon.Id;
        }
        else
        {
            if (plan.DefaultCouponId == coupon.Id)
                plan.DefaultCouponId = null;
        }

        await db.SaveChangesAsync();

        logger.LogInformation("Admin {Action} default coupon {CouponId} for plan {PlanId}",
            request.IsDefault ? "set" : "removed", id, plan.Id);

        return Ok(new { coupon.Id, IsDefaultForPlan = plan.DefaultCouponId == coupon.Id });
    }
}
