using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Data.Entities;
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
            AllowedPaths = agent.AllowedPathsJson is not null
                ? JsonSerializer.Deserialize<List<string>>(agent.AllowedPathsJson)
                : new List<string>()
        });
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
            .Where(c => c.AgentId == agentId && c.RevokedAt == null)
            .ToListAsync();

        foreach (var cred in activeCredentials)
        {
            cred.RevokedAt = DateTime.UtcNow;
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
            .FirstOrDefaultAsync(c => c.Id == credentialId && c.AgentId == agentId);

        if (credential is null) return NotFound();

        credential.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }
}
