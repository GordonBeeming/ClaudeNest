using System.Security.Cryptography;
using System.Text;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Data.Entities;
using ClaudeNest.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PairingController(NestDbContext db, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// Web client calls this to generate a pairing token for a new agent.
    /// Requires Auth0 JWT.
    /// </summary>
    [HttpPost("generate")]
    [Authorize]
    public async Task<IActionResult> GeneratePairingToken()
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();

        var user = await db.Users
            .Include(u => u.Account)
            .ThenInclude(a => a.Plan)
            .FirstOrDefaultAsync(u => u.Auth0UserId == auth0UserId);
        if (user is null) return NotFound("User not found");

        // Check subscription is active or trialing
        var status = user.Account.SubscriptionStatus;
        if (status != SubscriptionStatus.Active && status != SubscriptionStatus.Trialing)
            return BadRequest("Active subscription required to pair agents");

        if (status == SubscriptionStatus.Trialing)
        {
            var now = timeProvider.GetUtcNow();
            var hasActiveTrial = await db.CouponRedemptions
                .AnyAsync(cr => cr.AccountId == user.AccountId && cr.FreeUntil > now);
            if (!hasActiveTrial)
                return BadRequest("Trial has expired");
        }

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes);
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));

        var pairingToken = new PairingToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = timeProvider.GetUtcNow().AddMinutes(10)
        };

        db.PairingTokens.Add(pairingToken);
        await db.SaveChangesAsync();

        return Ok(new { token });
    }

    /// <summary>
    /// Agent calls this to exchange a pairing token for permanent credentials.
    /// No auth required — the token IS the auth.
    /// </summary>
    [HttpPost("exchange")]
    [AllowAnonymous]
    public async Task<IActionResult> ExchangePairingToken([FromBody] ExchangeRequest request)
    {
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Token));

        var now = timeProvider.GetUtcNow();
        var pairingToken = await db.PairingTokens
            .AsTracking()
            .Include(t => t.User)
            .ThenInclude(u => u.Account)
            .ThenInclude(a => a.Plan)
            .FirstOrDefaultAsync(t => t.RedeemedAt == null && t.ExpiresAt > now);

        if (pairingToken is null ||
            !CryptographicOperations.FixedTimeEquals(pairingToken.TokenHash, tokenHash))
        {
            return BadRequest("Invalid or expired pairing token");
        }

        var user = pairingToken.User;
        var account = user.Account;

        // Check subscription status
        var status = account.SubscriptionStatus;
        if (status != SubscriptionStatus.Active && status != SubscriptionStatus.Trialing)
            return BadRequest("Active subscription required to pair agents");

        if (status == SubscriptionStatus.Trialing)
        {
            var hasActiveTrial = await db.CouponRedemptions
                .AnyAsync(cr => cr.AccountId == account.Id && cr.FreeUntil > now);
            if (!hasActiveTrial)
                return BadRequest("Trial has expired");
        }

        // Enforce max agents
        var maxAgents = account.Plan?.MaxAgents ?? 0;
        var currentAgentCount = await db.Agents.CountAsync(a => a.AccountId == account.Id);
        if (currentAgentCount >= maxAgents)
            return BadRequest("Agent limit reached for your plan");

        // Create the agent
        var agent = new Data.Entities.Agent
        {
            AccountId = user.AccountId,
            Name = request.AgentName,
            Hostname = request.Hostname,
            OS = request.OS
        };
        db.Agents.Add(agent);

        // Create the credential
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var secret = Convert.ToBase64String(secretBytes);
        var secretHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));

        var credential = new AgentCredential
        {
            Agent = agent,
            SecretHash = secretHash
        };
        db.AgentCredentials.Add(credential);

        // Burn the pairing token
        pairingToken.RedeemedAt = now;

        await db.SaveChangesAsync();

        return Ok(new
        {
            agentId = agent.Id,
            secret
        });
    }
}

public record ExchangeRequest(string Token, string? AgentName, string? Hostname, string? OS);
