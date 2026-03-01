using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeController(NestDbContext db) : ControllerBase
{
    /// <summary>
    /// Returns the current user's profile.
    /// Auto-provisions a user record on first login.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMe()
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var email = User.FindFirst("email")?.Value ?? "unknown@unknown";
        var name = User.FindFirst("name")?.Value;

        var user = await db.Users
            .Include(u => u.Account)
            .ThenInclude(a => a.Plan)
            .FirstOrDefaultAsync(u => u.Auth0UserId == auth0UserId);

        if (user is null)
        {
            var account = new Account
            {
                Name = name ?? email
            };
            db.Accounts.Add(account);

            user = new User
            {
                Auth0UserId = auth0UserId,
                Email = email,
                DisplayName = name,
                AccountId = account.Id
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // Reload with includes
            user = await db.Users
                .Include(u => u.Account)
                .ThenInclude(a => a.Plan)
                .FirstAsync(u => u.Id == user.Id);
        }

        var account2 = user.Account;

        return Ok(new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            Account = new
            {
                account2.Id,
                PlanId = account2.PlanId,
                PlanName = account2.Plan?.Name,
                SubscriptionStatus = account2.SubscriptionStatus.ToString(),
                account2.TrialEndsAt,
                MaxAgents = account2.Plan?.MaxAgents ?? 0,
                MaxSessions = account2.Plan?.MaxSessions ?? 0
            }
        });
    }
}
