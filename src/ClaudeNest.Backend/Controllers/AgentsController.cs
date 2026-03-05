using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Data.Entities;
using ClaudeNest.Backend.Hubs;
using ClaudeNest.Shared.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AgentsController(NestDbContext db, IHubContext<NestHub> hubContext, TimeProvider timeProvider, IConfiguration configuration, IHttpClientFactory httpClientFactory, IMemoryCache memoryCache) : ControllerBase
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

        var repoOwner = configuration["Agent:GitHubRepoOwner"] ?? "gordonbeeming";
        var repoName = configuration["Agent:GitHubRepoName"] ?? "ClaudeNest";

        // Fetch latest release from GitHub API (cached for 5 minutes)
        var version = configuration["Agent:LatestVersion"];
        string? downloadUrl = null;

        if (string.IsNullOrEmpty(version) || version == "1.0.0")
        {
            const string cacheKey = "github:latest-agent-release";
            var agentRelease = await memoryCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                try
                {
                    var http = httpClientFactory.CreateClient();
                    http.DefaultRequestHeaders.Add("User-Agent", "ClaudeNest-Backend");
                    http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

                    var response = await http.GetAsync($"https://api.github.com/repos/{repoOwner}/{repoName}/releases?per_page=10");
                    response.EnsureSuccessStatusCode();

                    var releases = await response.Content.ReadFromJsonAsync<List<GitHubRelease>>();
                    return releases?.FirstOrDefault(r => r.TagName?.StartsWith("agent-v") == true);
                }
                catch
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
                    return null;
                }
            });

            if (agentRelease is not null)
            {
                version = agentRelease.TagName!["agent-v".Length..];
                downloadUrl = $"https://github.com/{repoOwner}/{repoName}/releases/download/{agentRelease.TagName}/";
            }
        }

        version ??= "1.0.0";
        downloadUrl ??= $"https://github.com/{repoOwner}/{repoName}/releases/download/agent-v{version}/";

        var notification = new UpdateAvailableNotification
        {
            LatestVersion = version,
            DownloadUrl = downloadUrl,
            IsForced = false
        };

        var connId = await db.Agents
            .Where(a => a.Id == agentId && a.IsOnline)
            .Select(a => a.ConnectionId)
            .FirstOrDefaultAsync();

        if (connId is not null)
        {
            await hubContext.Clients.Client(connId)
                .SendAsync("TriggerUpdate", notification);
            return Ok(new { message = "Update triggered" });
        }

        return BadRequest(new { message = "Agent is offline" });
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }
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
        var connIdForDeregister = await db.Agents
            .Where(a => a.Id == agentId && a.IsOnline)
            .Select(a => a.ConnectionId)
            .FirstOrDefaultAsync();

        if (connIdForDeregister is not null)
        {
            try
            {
                await hubContext.Clients.Client(connIdForDeregister)
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

        var folderPreferences = await db.UserFolderPreferences
            .AsTracking()
            .Where(p => p.AgentId == agentId)
            .ToListAsync();
        db.UserFolderPreferences.RemoveRange(folderPreferences);
        db.Sessions.RemoveRange(agent.Sessions);
        db.AgentCredentials.RemoveRange(agent.Credentials);
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
