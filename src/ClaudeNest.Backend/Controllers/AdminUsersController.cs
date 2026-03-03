using ClaudeNest.Backend.Auth;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Stripe;
using ClaudeNest.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize]
[AdminRequired]
public class AdminUsersController(NestDbContext db, IStripeService stripeService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListUsers(
        [FromQuery] string? domain,
        [FromQuery] bool? hasCoupon,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var now = DateTimeOffset.UtcNow;

        var activeDeals = await db.CompanyDeals
            .Where(d => d.IsActive)
            .Select(d => d.Domain)
            .ToListAsync();

        var query = db.Users
            .Include(u => u.Account)
                .ThenInclude(a => a!.Plan)
            .Include(u => u.Account)
                .ThenInclude(a => a!.CouponRedemptions)
                    .ThenInclude(r => r.Coupon)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(domain))
        {
            var domainLower = domain.ToLowerInvariant();
            query = query.Where(u => u.Email.ToLower().EndsWith("@" + domainLower));
        }

        if (hasCoupon == true)
        {
            query = query.Where(u => u.Account.CouponRedemptions.Any(r => r.FreeUntil > now));
        }
        else if (hasCoupon == false)
        {
            query = query.Where(u => !u.Account.CouponRedemptions.Any(r => r.FreeUntil > now));
        }

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = users.Select(u =>
        {
            var activeRedemption = u.Account.CouponRedemptions
                .FirstOrDefault(r => r.FreeUntil > now);

            var emailDomain = u.Email.Contains('@') ? u.Email.Split('@')[1].ToLowerInvariant() : null;
            var matchedDeal = emailDomain != null && activeDeals.Contains(emailDomain) ? emailDomain : null;

            return new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.IsAdmin,
                u.CreatedAt,
                AccountId = u.AccountId,
                AccountName = u.Account.Name,
                PlanName = u.Account.Plan?.Name,
                SubscriptionStatus = u.Account.SubscriptionStatus.ToString(),
                u.Account.CurrentPeriodEnd,
                u.Account.CancelAtPeriodEnd,
                HasBillingAccount = u.Account.StripeCustomerId != null,
                HasStripeSubscription = u.Account.StripeSubscriptionId != null,
                ActiveCoupon = activeRedemption != null ? new
                {
                    CouponId = activeRedemption.CouponId,
                    Code = activeRedemption.Coupon.Code,
                    FreeUntil = activeRedemption.FreeUntil
                } : null,
                CompanyDealDomain = matchedDeal
            };
        }).ToList();

        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpPost("{id:guid}/cancel-subscription")]
    public async Task<IActionResult> CancelSubscription(Guid id)
    {
        var user = await db.Users
            .AsTracking()
            .Include(u => u.Account)
                .ThenInclude(a => a!.Plan)
            .Include(u => u.Account)
                .ThenInclude(a => a!.CouponRedemptions)
                    .ThenInclude(r => r.Coupon)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null) return NotFound();

        if (string.IsNullOrEmpty(user.Account.StripeSubscriptionId))
            return BadRequest("User does not have an active Stripe subscription");

        try
        {
            await stripeService.CancelSubscriptionAsync(user.Account.StripeSubscriptionId);
        }
        catch
        {
            // Continue even if Stripe call fails (e.g. dev mode)
        }

        user.Account.SubscriptionStatus = SubscriptionStatus.Cancelled;
        user.Account.CancelAtPeriodEnd = false;
        user.Account.StripeSubscriptionId = null;

        await db.SaveChangesAsync();

        return Ok(BuildUserResponse(user));
    }

    [HttpPost("{id:guid}/toggle-admin")]
    public async Task<IActionResult> ToggleAdmin(Guid id)
    {
        var callerAuth0Id = User.FindFirst("sub")?.Value;
        if (callerAuth0Id is null) return Unauthorized();

        var caller = await db.Users.FirstOrDefaultAsync(u => u.Auth0UserId == callerAuth0Id);
        if (caller is null) return Unauthorized();

        if (caller.Id == id)
            return BadRequest("You cannot change your own admin status");

        var user = await db.Users
            .AsTracking()
            .Include(u => u.Account)
                .ThenInclude(a => a!.Plan)
            .Include(u => u.Account)
                .ThenInclude(a => a!.CouponRedemptions)
                    .ThenInclude(r => r.Coupon)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null) return NotFound();

        user.IsAdmin = !user.IsAdmin;
        await db.SaveChangesAsync();

        return Ok(BuildUserResponse(user));
    }

    [HttpPost("{id:guid}/give-coupon")]
    public async Task<IActionResult> GiveCoupon(Guid id, [FromBody] GiveCouponRequest request)
    {
        var user = await db.Users
            .AsTracking()
            .Include(u => u.Account)
                .ThenInclude(a => a!.Plan)
            .Include(u => u.Account)
                .ThenInclude(a => a!.CouponRedemptions)
                    .ThenInclude(r => r.Coupon)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null) return NotFound();

        var coupon = await db.Coupons.AsTracking().Include(c => c.Plan).FirstOrDefaultAsync(c => c.Id == request.CouponId);
        if (coupon is null) return BadRequest("Coupon not found");
        if (!coupon.IsActive) return BadRequest("Coupon is not active");

        var now = DateTimeOffset.UtcNow;
        var freeUntil = coupon.DiscountType switch
        {
            DiscountType.FreeDays => now.AddDays(coupon.FreeDays ?? 0),
            DiscountType.PercentOff or DiscountType.AmountOff => now.AddMonths(coupon.DurationMonths),
            _ => now.AddMonths(coupon.FreeMonths)
        };

        var redemption = new Data.Entities.CouponRedemption
        {
            CouponId = coupon.Id,
            AccountId = user.AccountId,
            FreeUntil = freeUntil
        };

        coupon.TimesRedeemed++;
        db.CouponRedemptions.Add(redemption);

        // Update the account plan to the coupon's plan
        user.Account.PlanId = coupon.PlanId;
        if (user.Account.SubscriptionStatus == SubscriptionStatus.None)
        {
            user.Account.SubscriptionStatus = SubscriptionStatus.Active;
        }

        await db.SaveChangesAsync();

        // Reload to get updated coupon data
        await db.Entry(redemption).Reference(r => r.Coupon).LoadAsync();

        return Ok(BuildUserResponse(user));
    }

    private object BuildUserResponse(Data.Entities.User user)
    {
        var now = DateTimeOffset.UtcNow;
        var activeRedemption = user.Account.CouponRedemptions
            .FirstOrDefault(r => r.FreeUntil > now);

        var emailDomain = user.Email.Contains('@') ? user.Email.Split('@')[1].ToLowerInvariant() : null;

        return new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.IsAdmin,
            user.CreatedAt,
            AccountId = user.AccountId,
            AccountName = user.Account.Name,
            PlanName = user.Account.Plan?.Name,
            SubscriptionStatus = user.Account.SubscriptionStatus.ToString(),
            user.Account.CurrentPeriodEnd,
            user.Account.CancelAtPeriodEnd,
            HasBillingAccount = user.Account.StripeCustomerId != null,
            HasStripeSubscription = user.Account.StripeSubscriptionId != null,
            ActiveCoupon = activeRedemption != null ? new
            {
                CouponId = activeRedemption.CouponId,
                Code = activeRedemption.Coupon.Code,
                FreeUntil = activeRedemption.FreeUntil
            } : null,
            CompanyDealDomain = emailDomain
        };
    }
}

public record GiveCouponRequest(Guid CouponId);
