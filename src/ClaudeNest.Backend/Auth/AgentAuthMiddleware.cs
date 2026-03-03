using System.Security.Cryptography;
using System.Text;
using ClaudeNest.Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Auth;

public class AgentAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, NestDbContext db, TimeProvider timeProvider)
    {
        // Only apply to the SignalR hub negotiate endpoint for agents
        if (!context.Request.Path.StartsWithSegments("/hubs/nest"))
        {
            await next(context);
            return;
        }

        // Check for agent auth headers
        var agentIdHeader = context.Request.Headers["X-Agent-Id"].FirstOrDefault();
        var agentSecretHeader = context.Request.Headers["X-Agent-Secret"].FirstOrDefault();

        if (string.IsNullOrEmpty(agentIdHeader) || string.IsNullOrEmpty(agentSecretHeader))
        {
            // Not an agent connection — let JWT auth handle it
            await next(context);
            return;
        }

        if (!Guid.TryParse(agentIdHeader, out var agentId))
        {
            context.Response.StatusCode = 401;
            return;
        }

        var secretHash = SHA256.HashData(Encoding.UTF8.GetBytes(agentSecretHeader));

        var credential = await db.AgentCredentials
            .Where(c => c.AgentId == agentId && c.RevokedAt == null)
            .FirstOrDefaultAsync();

        if (credential is null || !CryptographicOperations.FixedTimeEquals(credential.SecretHash, secretHash))
        {
            context.Response.StatusCode = 401;
            return;
        }

        // Update last used
        credential.LastUsedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync();

        // Store agent ID in connection context for the hub
        context.Items["AgentId"] = agentId;

        await next(context);
    }
}
