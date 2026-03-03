using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Data.Entities;
using ClaudeNest.Backend.Hubs;
using ClaudeNest.Shared.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AgentsController(NestDbContext db, IHubContext<NestHub> hubContext, TimeProvider timeProvider, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAgents()
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var agents = await db.Agents
            .Where(a => a.Account.Users.Any(u => u.Auth0UserId == auth0UserId))
            .Include(a => a.Account)
            .ThenInclude(a => a.Plan)
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Hostname,
                a.OS,
                a.IsOnline,
                a.LastSeenAt,
                a.CreatedAt,
                a.AllowedPathsJson,
                a.Version,
                a.Architecture,
                MaxSessions = a.Account.Plan != null ? a.Account.Plan.MaxSessions : 0,
                MaxAgents = a.Account.Plan != null ? a.Account.Plan.MaxAgents : 0
            })
            .ToListAsync();

        return Ok(agents.Select(a => new
        {
            a.Id,
            a.Name,
            a.Hostname,
            a.OS,
            a.IsOnline,
            a.LastSeenAt,
            a.CreatedAt,
            a.MaxSessions,
            a.MaxAgents,
            a.Version,
            a.Architecture,
            AllowedPaths = a.AllowedPathsJson is not null
                ? JsonSerializer.Deserialize<List<string>>(a.AllowedPathsJson)
                : new List<string>()
        }));
    }

    [HttpGet("{agentId:guid}")]
    public async Task<IActionResult> GetAgent(Guid agentId)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var agent = await db.Agents
            .Where(a => a.Id == agentId && a.Account.Users.Any(u => u.Auth0UserId == auth0UserId))
            .Include(a => a.Account)
            .ThenInclude(a => a.Plan)
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Hostname,
                a.OS,
                a.IsOnline,
                a.LastSeenAt,
                a.CreatedAt,
                a.AllowedPathsJson,
                a.Version,
                a.Architecture,
                MaxSessions = a.Account.Plan != null ? a.Account.Plan.MaxSessions : 0,
                MaxAgents = a.Account.Plan != null ? a.Account.Plan.MaxAgents : 0
            })
            .FirstOrDefaultAsync();

        if (agent is null) return NotFound();

        return Ok(new
        {
            agent.Id,
            agent.Name,
            agent.Hostname,
            agent.OS,
            agent.IsOnline,
            agent.LastSeenAt,
            agent.CreatedAt,
            agent.MaxSessions,
            agent.MaxAgents,
            agent.Version,
            agent.Architecture,
            AllowedPaths = agent.AllowedPathsJson is not null
                ? JsonSerializer.Deserialize<List<string>>(agent.AllowedPathsJson)
                : new List<string>()
        });
    }

    [HttpPost("{agentId:guid}/update")]
    public async Task<IActionResult> TriggerUpdate(Guid agentId)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var agent = await db.Agents
            .Where(a => a.Id == agentId && a.Account.Users.Any(u => u.Auth0UserId == auth0UserId))
            .FirstOrDefaultAsync();

        if (agent is null) return NotFound();

        var version = configuration["Agent:LatestVersion"] ?? "1.0.0";
        var notification = new UpdateAvailableNotification
        {
            LatestVersion = version,
            DownloadUrl = $"https://github.com/gordonbeeming/ClaudeNest/releases/download/agent-v{version}/",
            IsForced = false
        };

        if (NestHub.TryGetAgentConnectionId(agentId, out var connectionId))
        {
            await hubContext.Clients.Client(connectionId)
                .SendAsync("TriggerUpdate", notification);
            return Ok(new { message = "Update triggered" });
        }

        return BadRequest(new { message = "Agent is offline" });
    }

    [HttpGet("{agentId:guid}/credentials")]
    public async Task<IActionResult> GetCredentials(Guid agentId)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var agent = await db.Agents
            .Where(a => a.Id == agentId && a.Account.Users.Any(u => u.Auth0UserId == auth0UserId))
            .FirstOrDefaultAsync();

        if (agent is null) return NotFound();

        var credentials = await db.AgentCredentials
            .Where(c => c.AgentId == agentId)
            .OrderByDescending(c => c.IssuedAt)
            .Select(c => new
            {
                c.Id,
                c.IssuedAt,
                c.LastUsedAt,
                c.RevokedAt,
                IsActive = c.RevokedAt == null
            })
            .ToListAsync();

        return Ok(credentials);
    }

    [HttpPost("{agentId:guid}/rotate-secret")]
    public async Task<IActionResult> RotateSecret(Guid agentId)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var agent = await db.Agents
            .Where(a => a.Id == agentId && a.Account.Users.Any(u => u.Auth0UserId == auth0UserId))
            .FirstOrDefaultAsync();

        if (agent is null) return NotFound();

        // Revoke all active credentials
        var activeCredentials = await db.AgentCredentials
            .AsTracking()
            .Where(c => c.AgentId == agentId && c.RevokedAt == null)
            .ToListAsync();

        var now = timeProvider.GetUtcNow();
        foreach (var cred in activeCredentials)
        {
            cred.RevokedAt = now;
        }

        // Create new credential
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var secret = Convert.ToBase64String(secretBytes);
        var secretHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));

        var newCredential = new AgentCredential
        {
            AgentId = agentId,
            SecretHash = secretHash
        };
        db.AgentCredentials.Add(newCredential);

        await db.SaveChangesAsync();

        return Ok(new
        {
            credentialId = newCredential.Id,
            secret
        });
    }

    [HttpDelete("{agentId:guid}")]
    public async Task<IActionResult> DeleteAgent(Guid agentId)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var agent = await db.Agents
            .AsTracking()
            .Include(a => a.Credentials)
            .Include(a => a.Sessions)
            .Where(a => a.Id == agentId && a.Account.Users.Any(u => u.Auth0UserId == auth0UserId))
            .FirstOrDefaultAsync();

        if (agent is null) return NotFound();

        // If agent is online, send a best-effort Deregister command
        if (NestHub.TryGetAgentConnectionId(agentId, out var connectionId))
        {
            try
            {
                await hubContext.Clients.Client(connectionId)
                    .SendAsync("Deregister", new DeregisterCommand
                    {
                        AgentId = agentId,
                        Reason = "Agent removed by user"
                    });
            }
            catch
            {
                // Best-effort — agent may have disconnected between the check and the send
            }
        }

        db.Agents.Remove(agent);
        await db.SaveChangesAsync();

        // Notify web clients watching this agent
        await hubContext.Clients.Group($"user:{agentId}")
            .SendAsync("AgentRemoved", agentId);

        return NoContent();
    }

    [HttpDelete("{agentId:guid}/credentials/{credentialId:guid}")]
    public async Task<IActionResult> RevokeCredential(Guid agentId, Guid credentialId)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var agent = await db.Agents
            .Where(a => a.Id == agentId && a.Account.Users.Any(u => u.Auth0UserId == auth0UserId))
            .FirstOrDefaultAsync();

        if (agent is null) return NotFound();

        var credential = await db.AgentCredentials
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Id == credentialId && c.AgentId == agentId);

        if (credential is null) return NotFound();

        credential.RevokedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync();

        return NoContent();
    }
}
