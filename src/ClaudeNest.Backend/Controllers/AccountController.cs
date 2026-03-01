using ClaudeNest.Backend.Data;
using ClaudeNest.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountController(NestDbContext db) : ControllerBase
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

        return Ok(new
        {
            account.Id,
            account.Name,
            PlanId = account.PlanId,
            PlanName = account.Plan?.Name,
            SubscriptionStatus = account.SubscriptionStatus.ToString(),
            account.TrialEndsAt,
            MaxAgents = account.Plan?.MaxAgents ?? 0,
            MaxSessions = account.Plan?.MaxSessions ?? 0,
            AgentCount = agentCount,
            ActiveSessionCount = activeSessionCount,
            account.PermissionMode
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
            .Include(u => u.Account)
            .FirstOrDefaultAsync(u => u.Auth0UserId == auth0UserId);

        if (user is null) return NotFound();

        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive);
        if (plan is null) return BadRequest("Invalid plan");

        var account = user.Account;
        account.PlanId = plan.Id;

        if (plan.TrialDays > 0)
        {
            account.SubscriptionStatus = SubscriptionStatus.Trialing;
            account.TrialEndsAt = DateTime.UtcNow.AddDays(plan.TrialDays);
        }
        else
        {
            account.SubscriptionStatus = SubscriptionStatus.Active;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            account.Id,
            account.PlanId,
            PlanName = plan.Name,
            SubscriptionStatus = account.SubscriptionStatus.ToString(),
            account.TrialEndsAt,
            MaxAgents = plan.MaxAgents,
            MaxSessions = plan.MaxSessions
        });
    }
}

public record SelectPlanRequest(Guid PlanId);
public record SetPermissionModeRequest(string Mode);
