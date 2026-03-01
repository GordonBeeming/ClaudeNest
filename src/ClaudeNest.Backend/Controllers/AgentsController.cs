using ClaudeNest.Backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AgentsController(NestDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAgents()
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var agents = await db.Agents
            .Where(a => a.User.Auth0UserId == auth0UserId)
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Hostname,
                a.OS,
                a.IsOnline,
                a.LastSeenAt,
                a.CreatedAt
            })
            .ToListAsync();

        return Ok(agents);
    }

    [HttpGet("{agentId:guid}")]
    public async Task<IActionResult> GetAgent(Guid agentId)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var agent = await db.Agents
            .Where(a => a.Id == agentId && a.User.Auth0UserId == auth0UserId)
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Hostname,
                a.OS,
                a.IsOnline,
                a.LastSeenAt,
                a.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (agent is null) return NotFound();

        return Ok(agent);
    }
}
