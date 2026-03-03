using ClaudeNest.Backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/account/ledger")]
[Authorize]
public class AccountLedgerController(NestDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetLedger([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Auth0UserId == auth0UserId);
        if (user is null) return NotFound();

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        var query = db.AccountLedger
            .Where(e => e.AccountId == user.AccountId)
            .OrderByDescending(e => e.CreatedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                EntryType = e.EntryType.ToString(),
                e.AmountCents,
                e.Currency,
                e.Description,
                e.PlanId,
                e.StripeInvoiceId,
                e.CouponId,
                e.CompanyDealId,
                e.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }
}
