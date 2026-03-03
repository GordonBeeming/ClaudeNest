using ClaudeNest.Backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlansController(NestDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPlans()
    {
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
                DefaultCoupon = p.DefaultCoupon != null && p.DefaultCoupon.IsActive
                    ? new { p.DefaultCoupon.FreeMonths }
                    : null
            })
            .ToListAsync();

        return Ok(plans);
    }
}
