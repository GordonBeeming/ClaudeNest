using ClaudeNest.Backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SessionsController(NestDbContext db) : ControllerBase
{
    [HttpGet("agent/{agentId:guid}")]
    public async Task<IActionResult> GetSessions(Guid agentId)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        // Verify the agent belongs to this user
        var agentBelongsToUser = await db.Agents
            .AnyAsync(a => a.Id == agentId && a.User.Auth0UserId == auth0UserId);

        if (!agentBelongsToUser) return NotFound();

        var sessions = await db.Sessions
            .Where(s => s.AgentId == agentId)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new
            {
                s.Id,
                s.AgentId,
                s.Path,
                s.State,
                s.Pid,
                s.StartedAt,
                s.EndedAt,
                s.ExitCode
            })
            .ToListAsync();

        return Ok(sessions);
    }
}
