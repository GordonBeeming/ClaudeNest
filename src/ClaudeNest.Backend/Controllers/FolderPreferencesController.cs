using System.Text.RegularExpressions;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Data.Entities;
using ClaudeNest.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/agents/{agentId:guid}/folder-preferences")]
[Authorize]
public partial class FolderPreferencesController(NestDbContext db, TimeProvider timeProvider) : ControllerBase
{
    [GeneratedRegex(@"^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColorRegex();

    [HttpGet]
    public async Task<IActionResult> GetPreferences(Guid agentId)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Auth0UserId == auth0UserId);
        if (user is null) return Unauthorized();

        // Verify user has access to this agent
        var agentExists = await db.Agents
            .AnyAsync(a => a.Id == agentId && a.Account.Users.Any(u => u.Auth0UserId == auth0UserId));
        if (!agentExists) return NotFound();

        var preferences = await db.UserFolderPreferences
            .Where(p => p.UserId == user.Id && p.AgentId == agentId)
            .Select(p => new
            {
                p.Id,
                p.Path,
                p.IsFavorite,
                p.Color,
                p.UpdatedAt
            })
            .ToListAsync();

        return Ok(preferences);
    }

    [HttpPut]
    public async Task<IActionResult> UpsertPreference(Guid agentId, [FromBody] UpsertFolderPreferenceRequest request)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Auth0UserId == auth0UserId);
        if (user is null) return Unauthorized();

        var agentExists = await db.Agents
            .AnyAsync(a => a.Id == agentId && a.Account.Users.Any(u => u.Auth0UserId == auth0UserId));
        if (!agentExists) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Path))
            return BadRequest(new { message = "Path is required" });

        if (request.Color is not null && !HexColorRegex().IsMatch(request.Color))
            return BadRequest(new { message = "Color must be a hex color like #ff5733" });

        var now = timeProvider.GetUtcNow();

        var existing = await db.UserFolderPreferences
            .AsTracking()
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.AgentId == agentId && p.Path == request.Path);

        if (existing is not null)
        {
            existing.IsFavorite = request.IsFavorite;
            existing.Color = request.Color;
            existing.UpdatedAt = now;
        }
        else
        {
            existing = new UserFolderPreference
            {
                UserId = user.Id,
                AgentId = agentId,
                Path = request.Path,
                IsFavorite = request.IsFavorite,
                Color = request.Color,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.UserFolderPreferences.Add(existing);
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            existing.Id,
            existing.Path,
            existing.IsFavorite,
            existing.Color,
            existing.UpdatedAt
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePreference(Guid agentId, Guid id)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Auth0UserId == auth0UserId);
        if (user is null) return Unauthorized();

        var preference = await db.UserFolderPreferences
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == user.Id && p.AgentId == agentId);

        if (preference is null) return NotFound();

        db.UserFolderPreferences.Remove(preference);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
