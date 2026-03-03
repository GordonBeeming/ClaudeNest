using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Stripe;
using ClaudeNest.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountController(NestDbContext db, IStripeService stripeService, TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAccount()
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var user = await db.Users
            .Include(u => u.Account)
            .ThenInclude(a => a.Plan)
            .FirstOrDefaultAsync(u => u.Auth0UserId == auth0UserId);

        if (user is null) return NotFound();

        var account = user.Account;
        var agentCount = await db.Agents.CountAsync(a => a.AccountId == account.Id);
        var activeSessionCount = await db.Sessions.CountAsync(s =>
            s.Agent.AccountId == account.Id &&
            (s.State == "Running" || s.State == "Starting" || s.State == "Requested"));

        // Check for active coupon redemptions
        var now = timeProvider.GetUtcNow();
        var activeCoupon = await db.CouponRedemptions
            .Include(cr => cr.Coupon)
            .Where(cr => cr.AccountId == account.Id && cr.FreeUntil > now)
            .OrderByDescending(cr => cr.FreeUntil)
            .Select(cr => new
            {
                cr.CouponId,
                cr.Coupon.Code,
                cr.FreeUntil
            })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            account.Id,
            account.Name,
            PlanId = account.PlanId,
            PlanName = account.Plan?.Name,
            SubscriptionStatus = account.SubscriptionStatus.ToString(),
            MaxAgents = account.Plan?.MaxAgents ?? 0,
            MaxSessions = account.Plan?.MaxSessions ?? 0,
            AgentCount = agentCount,
            ActiveSessionCount = activeSessionCount,
            account.PermissionMode,
            HasBillingAccount = account.StripeCustomerId != null,
            HasStripeSubscription = account.StripeSubscriptionId != null,
            ActiveCoupon = activeCoupon,
            account.CurrentPeriodEnd,
            account.CancelAtPeriodEnd
        });
    }

    private static readonly HashSet<string> ValidPermissionModes = ["default", "acceptEdits", "dontAsk", "bypassPermissions", "plan"];

    [HttpPost("permission-mode")]
    public async Task<IActionResult> SetPermissionMode([FromBody] SetPermissionModeRequest request)
    {
        if (!ValidPermissionModes.Contains(request.Mode))
            return BadRequest("Invalid permission mode");

        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var user = await db.Users
            .AsTracking()
            .Include(u => u.Account)
            .FirstOrDefaultAsync(u => u.Auth0UserId == auth0UserId);

        if (user is null) return NotFound();

        user.Account.PermissionMode = request.Mode;
        await db.SaveChangesAsync();

        return Ok(new { PermissionMode = request.Mode });
    }

    [HttpPost("select-plan")]
    public async Task<IActionResult> SelectPlan([FromBody] SelectPlanRequest request)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var user = await db.Users
            .AsTracking()
            .Include(u => u.Account)
            .FirstOrDefaultAsync(u => u.Auth0UserId == auth0UserId);

        if (user is null) return NotFound();

        var plan = await db.Plans
            .Include(p => p.DefaultCoupon)
            .FirstOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive);
        if (plan is null) return BadRequest("Invalid plan");

        var account = user.Account;

        // Block plan changes while an active Stripe subscription exists (unless cancelling at period end)
        if (!string.IsNullOrEmpty(account.StripeSubscriptionId) &&
            account.SubscriptionStatus is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.PastDue &&
            !account.CancelAtPeriodEnd)
        {
            return BadRequest("Cannot change plan while you have an active subscription. Please cancel your current plan first via the billing portal, then select a new plan after it expires.");
        }

        // If plan has a Stripe price, redirect to Stripe Checkout
        if (!string.IsNullOrEmpty(plan.StripePriceId))
        {
            var customerId = await stripeService.GetOrCreateCustomerAsync(
                user.Email, user.DisplayName ?? user.Email, account.Id);

            account.StripeCustomerId = customerId;
            account.PlanId = plan.Id;
            await db.SaveChangesAsync();

            string? stripeCouponId = null;
            Guid? couponId = null;

            // Check for coupon
            if (!string.IsNullOrEmpty(request.CouponCode))
            {
                var couponCode = request.CouponCode.Trim().ToUpperInvariant();
                var coupon = await db.Coupons.FirstOrDefaultAsync(c =>
                    c.Code == couponCode && c.IsActive && c.PlanId == plan.Id);

                var selectNow = timeProvider.GetUtcNow();
                if (coupon is not null &&
                    coupon.TimesRedeemed < coupon.MaxRedemptions &&
                    (coupon.ExpiresAt == null || coupon.ExpiresAt > selectNow))
                {
                    // Trial abuse check for default coupons
                    if (plan.DefaultCouponId == coupon.Id &&
                        account.StripePaymentMethodFingerprint is not null)
                    {
                        var fingerprintUsed = await db.Accounts
                            .AnyAsync(a =>
                                a.Id != account.Id &&
                                a.StripePaymentMethodFingerprint == account.StripePaymentMethodFingerprint);

                        if (fingerprintUsed)
                        {
                            return BadRequest("This payment method has already been used for a trial");
                        }
                    }

                    stripeCouponId = coupon.StripeCouponId;
                    couponId = coupon.Id;
                }
            }
            else if (plan.DefaultCoupon is not null &&
                     plan.DefaultCoupon.IsActive &&
                     plan.DefaultCoupon.TimesRedeemed < plan.DefaultCoupon.MaxRedemptions)
            {
                // Auto-apply plan's default coupon
                var alreadyRedeemed = await db.CouponRedemptions
                    .AnyAsync(cr => cr.CouponId == plan.DefaultCouponId && cr.AccountId == account.Id);

                if (!alreadyRedeemed)
                {
                    stripeCouponId = plan.DefaultCoupon.StripeCouponId;
                    couponId = plan.DefaultCoupon.Id;
                }
            }

            var checkoutUrl = await stripeService.CreateCheckoutSessionAsync(
                customerId, plan.StripePriceId, stripeCouponId, null, null);

            return Ok(new
            {
                RedirectUrl = checkoutUrl,
                Action = "redirect"
            });
        }

        // No Stripe price — local assignment (dev mode / free plan)
        account.PlanId = plan.Id;
        account.SubscriptionStatus = SubscriptionStatus.Active;
        await db.SaveChangesAsync();

        var localAgentCount = await db.Agents.CountAsync(a => a.AccountId == account.Id);
        var localActiveSessionCount = await db.Sessions.CountAsync(s =>
            s.Agent.AccountId == account.Id &&
            (s.State == "Running" || s.State == "Starting" || s.State == "Requested"));

        return Ok(new
        {
            RedirectUrl = (string?)null,
            Action = "local",
            Account = new
            {
                account.Id,
                account.Name,
                account.PlanId,
                PlanName = plan.Name,
                SubscriptionStatus = account.SubscriptionStatus.ToString(),
                MaxAgents = plan.MaxAgents,
                MaxSessions = plan.MaxSessions,
                AgentCount = localAgentCount,
                ActiveSessionCount = localActiveSessionCount,
                account.PermissionMode,
                HasBillingAccount = account.StripeCustomerId != null,
                HasStripeSubscription = account.StripeSubscriptionId != null
            }
        });
    }

    [HttpPost("billing-portal")]
    public async Task<IActionResult> CreateBillingPortal()
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var user = await db.Users
            .Include(u => u.Account)
            .FirstOrDefaultAsync(u => u.Auth0UserId == auth0UserId);

        if (user is null) return NotFound();

        if (string.IsNullOrEmpty(user.Account.StripeCustomerId))
            return BadRequest("No billing account found");

        var url = await stripeService.CreateBillingPortalSessionAsync(user.Account.StripeCustomerId, null);

        return Ok(new { Url = url });
    }

    [HttpPost("redeem-coupon")]
    public async Task<IActionResult> RedeemCoupon([FromBody] RedeemCouponRequest request)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var user = await db.Users
            .Include(u => u.Account)
            .FirstOrDefaultAsync(u => u.Auth0UserId == auth0UserId);

        if (user is null) return NotFound();

        var code = request.Code.Trim().ToUpperInvariant();
        var coupon = await db.Coupons
            .Include(c => c.Plan)
            .FirstOrDefaultAsync(c => c.Code == code);

        if (coupon is null)
            return Ok(new { Valid = false, Reason = "Coupon not found" });

        if (!coupon.IsActive)
            return Ok(new { Valid = false, Reason = "Coupon is no longer active" });

        if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt.Value < timeProvider.GetUtcNow())
            return Ok(new { Valid = false, Reason = "Coupon has expired" });

        if (coupon.TimesRedeemed >= coupon.MaxRedemptions)
            return Ok(new { Valid = false, Reason = "Coupon has reached maximum redemptions" });

        var alreadyRedeemed = await db.CouponRedemptions
            .AnyAsync(cr => cr.CouponId == coupon.Id && cr.AccountId == user.AccountId);

        if (alreadyRedeemed)
            return Ok(new { Valid = false, Reason = "Coupon already redeemed on this account" });

        return Ok(new
        {
            Valid = true,
            CouponId = coupon.Id,
            coupon.Code,
            PlanId = coupon.PlanId,
            PlanName = coupon.Plan.Name,
            coupon.FreeMonths,
            DiscountType = coupon.DiscountType.ToString(),
            coupon.PercentOff,
            coupon.AmountOffCents,
            coupon.FreeDays,
            coupon.DurationMonths
        });
    }
}

public record SelectPlanRequest(Guid PlanId, string? CouponCode = null);
public record SetPermissionModeRequest(string Mode);
public record RedeemCouponRequest(string Code);
