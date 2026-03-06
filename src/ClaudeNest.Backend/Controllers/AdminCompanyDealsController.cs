using ClaudeNest.Backend.Auth;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Data.Entities;
using ClaudeNest.Backend.Models;
using ClaudeNest.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/admin/company-deals")]
[Authorize]
[AdminRequired]
public class AdminCompanyDealsController(NestDbContext db, TimeProvider timeProvider, ILogger<AdminCompanyDealsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListDeals()
    {
        var deals = await db.CompanyDeals
            .Include(d => d.Plan)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        // Compute user counts per domain in a single query
        var domains = deals.Select(d => d.Domain).ToList();
        var userStats = await db.Users
            .Where(u => domains.Any(domain => u.Email.ToLower().EndsWith("@" + domain)))
            .Select(u => new { u.Email, u.Account.PlanId, u.AccountId })
            .ToListAsync();

        var result = deals.Select(d =>
        {
            var domainSuffix = "@" + d.Domain;
            var domainUsers = userStats.Where(u => u.Email.ToLower().EndsWith(domainSuffix)).ToList();

            return new
            {
                d.Id,
                d.Domain,
                d.PlanId,
                PlanName = d.Plan.Name,
                d.IsActive,
                d.CreatedAt,
                d.DeactivatedAt,
                UserCount = domainUsers.Count,
                OverriddenCount = domainUsers.Count(u => u.PlanId != d.PlanId),
                AccountIds = domainUsers.Select(u => u.AccountId.ToString()).Distinct().ToList(),
            };
        }).ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDeal([FromBody] CreateCompanyDealRequest request)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Auth0UserId == auth0UserId);
        if (user is null) return Unauthorized();

        // Validate domain format
        var domain = request.Domain.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(domain) || !domain.Contains('.'))
            return BadRequest("Invalid domain format");

        // Check for duplicates
        if (await db.CompanyDeals.AnyAsync(d => d.Domain == domain))
            return BadRequest("A deal for this domain already exists");

        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == request.PlanId);
        if (plan is null) return BadRequest("Invalid plan");

        var deal = new CompanyDeal
        {
            Domain = domain,
            PlanId = request.PlanId,
            CreatedByUserId = user.Id
        };

        db.CompanyDeals.Add(deal);
        await db.SaveChangesAsync();

        logger.LogInformation("Admin created company deal for domain {Domain} with plan {PlanId}", domain, request.PlanId);

        return Ok(new
        {
            deal.Id,
            deal.Domain,
            deal.PlanId,
            PlanName = plan.Name,
            deal.IsActive,
            deal.CreatedAt,
            deal.DeactivatedAt
        });
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateDeal(Guid id, [FromBody] UpdateCompanyDealRequest request)
    {
        var deal = await db.CompanyDeals.AsTracking().Include(d => d.Plan).FirstOrDefaultAsync(d => d.Id == id);
        if (deal is null) return NotFound();
        if (!deal.IsActive) return BadRequest("Cannot edit an inactive deal");

        var newPlan = await db.Plans.FirstOrDefaultAsync(p => p.Id == request.PlanId);
        if (newPlan is null) return BadRequest("Invalid plan");

        var oldPlanId = deal.PlanId;
        deal.PlanId = request.PlanId;

        // Switch all accounts on this domain that were on the old plan (without Stripe subscription)
        var affectedAccounts = await db.Users
            .AsTracking()
            .Where(u => u.Email.EndsWith("@" + deal.Domain))
            .Select(u => u.Account)
            .Where(a => a.PlanId == oldPlanId && a.StripeSubscriptionId == null
                && a.SubscriptionStatus == SubscriptionStatus.Active)
            .Distinct()
            .ToListAsync();

        foreach (var account in affectedAccounts)
        {
            account.PlanId = request.PlanId;
        }

        await db.SaveChangesAsync();

        logger.LogInformation("Admin updated company deal {DealId} to plan {PlanId}", id, request.PlanId);

        return Ok(new
        {
            deal.Id,
            deal.Domain,
            deal.PlanId,
            PlanName = newPlan.Name,
            deal.IsActive,
            deal.CreatedAt,
            deal.DeactivatedAt,
            AffectedAccounts = affectedAccounts.Count
        });
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateDeal(Guid id)
    {
        var deal = await db.CompanyDeals.AsTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (deal is null) return NotFound();

        deal.IsActive = false;
        deal.DeactivatedAt = timeProvider.GetUtcNow();

        // Cancel accounts with matching email domain that don't have a Stripe subscription
        var affectedAccounts = await db.Users
            .AsTracking()
            .Where(u => u.Email.EndsWith("@" + deal.Domain))
            .Select(u => u.Account)
            .Where(a => a.StripeSubscriptionId == null)
            .Distinct()
            .ToListAsync();

        foreach (var account in affectedAccounts)
        {
            account.SubscriptionStatus = SubscriptionStatus.Cancelled;
        }

        await db.SaveChangesAsync();

        logger.LogInformation("Admin deactivated company deal {DealId}", id);

        return Ok(new
        {
            deal.Id,
            deal.IsActive,
            deal.DeactivatedAt,
            AffectedAccounts = affectedAccounts.Count
        });
    }
}
