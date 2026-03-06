using System.Security.Cryptography;
using System.Text;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Auth;

public class AgentAuthMiddleware(RequestDelegate next, ILogger<AgentAuthMiddleware> logger)
{
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromMinutes(5);

    public async Task InvokeAsync(HttpContext context, NestDbContext db, TimeProvider timeProvider)
    {
        // Only apply to the SignalR hub negotiate endpoint for agents
        if (!context.Request.Path.StartsWithSegments("/hubs/nest"))
        {
            await next(context);
            return;
        }

        var agentIdHeader = context.Request.Headers["X-Agent-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(agentIdHeader))
        {
            // Check for HMAC token in Authorization header or access_token query param.
            // SignalR WebSocket connections use ClientWebSocket directly, which bypasses
            // HttpMessageHandlerFactory — so custom HMAC headers aren't sent on the
            // WebSocket upgrade request. The agent sends the token via AccessTokenProvider instead.
            var hmacToken = ExtractAgentHmacToken(context);
            if (hmacToken is not null)
            {
                var parts = hmacToken.Split('|', 3);
                if (parts.Length == 3 && Guid.TryParse(parts[0], out var tokenAgentId))
                {
                    await HandleHmacAuth(context, db, timeProvider, tokenAgentId, parts[1], parts[2]);
                    return;
                }
            }

            // Not an agent connection — let JWT auth handle it
            await next(context);
            return;
        }

        if (!Guid.TryParse(agentIdHeader, out var agentId))
        {
            logger.LogWarning("Agent auth failed: invalid AgentId format '{AgentId}' from {IP}",
                agentIdHeader, context.GetClientIp());
            context.Response.StatusCode = 401;
            return;
        }

        // Try HMAC auth first (new protocol), then fall back to legacy plain secret
        var timestampHeader = context.Request.Headers["X-Agent-Timestamp"].FirstOrDefault();
        var signatureHeader = context.Request.Headers["X-Agent-Signature"].FirstOrDefault();
        var legacySecretHeader = context.Request.Headers["X-Agent-Secret"].FirstOrDefault();

        if (!string.IsNullOrEmpty(timestampHeader) && !string.IsNullOrEmpty(signatureHeader))
        {
            await HandleHmacAuth(context, db, timeProvider, agentId, timestampHeader, signatureHeader);
        }
        else if (!string.IsNullOrEmpty(legacySecretHeader))
        {
            await HandleLegacyAuth(context, db, timeProvider, agentId, legacySecretHeader);
        }
        else
        {
            // No auth headers — let JWT auth handle it
            await next(context);
            return;
        }
    }

    private async Task HandleHmacAuth(
        HttpContext context, NestDbContext db, TimeProvider timeProvider,
        Guid agentId, string timestampHeader, string signatureHeader)
    {
        // Validate timestamp freshness
        if (!DateTimeOffset.TryParse(timestampHeader, out var timestamp))
        {
            logger.LogWarning("Agent auth failed: invalid timestamp for agent {AgentId} from {IP}",
                agentId, context.GetClientIp());
            context.Response.StatusCode = 401;
            return;
        }

        var now = timeProvider.GetUtcNow();
        if (Math.Abs((now - timestamp).TotalMinutes) > TimestampTolerance.TotalMinutes)
        {
            logger.LogWarning("Agent auth failed: stale timestamp for agent {AgentId} from {IP} (drift: {DriftMinutes:F1}m)",
                agentId, context.GetClientIp(), (now - timestamp).TotalMinutes);
            context.Response.StatusCode = 401;
            return;
        }

        // Decode the provided signature
        byte[] providedSignature;
        try
        {
            providedSignature = Convert.FromBase64String(signatureHeader);
        }
        catch (FormatException)
        {
            logger.LogWarning("Agent auth failed: malformed signature for agent {AgentId} from {IP}",
                agentId, context.GetClientIp());
            context.Response.StatusCode = 401;
            return;
        }

        var credential = await db.AgentCredentials
            .AsTracking()
            .Where(c => c.AgentId == agentId && c.RevokedAt == null)
            .FirstOrDefaultAsync();

        if (credential is null)
        {
            logger.LogWarning("Agent auth failed: no active credential for agent {AgentId} from {IP}",
                agentId, context.GetClientIp());
            context.Response.StatusCode = 401;
            return;
        }

        // Recompute the expected HMAC: HMAC-SHA256(SecretHash, timestamp|agentId)
        var message = $"{timestampHeader}|{agentId}";
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var expectedSignature = HMACSHA256.HashData(credential.SecretHash, messageBytes);

        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, providedSignature))
        {
            logger.LogWarning("Agent auth failed: HMAC signature mismatch for agent {AgentId} from {IP}",
                agentId, context.GetClientIp());
            context.Response.StatusCode = 401;
            return;
        }

        // Valid — update last used and continue
        credential.LastUsedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync();
        context.Items["AgentId"] = agentId;
        await next(context);
    }

    private static string? ExtractAgentHmacToken(HttpContext context)
    {
        const string prefix = "agent-hmac|";

        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader is not null && authHeader.StartsWith($"Bearer {prefix}"))
            return authHeader[$"Bearer {prefix}".Length..];

        var accessToken = context.Request.Query["access_token"].FirstOrDefault();
        if (accessToken is not null && accessToken.StartsWith(prefix))
            return accessToken[prefix.Length..];

        return null;
    }

    private async Task HandleLegacyAuth(
        HttpContext context, NestDbContext db, TimeProvider timeProvider,
        Guid agentId, string secret)
    {
        var secretHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));

        var credential = await db.AgentCredentials
            .AsTracking()
            .Where(c => c.AgentId == agentId && c.RevokedAt == null)
            .FirstOrDefaultAsync();

        if (credential is null || !CryptographicOperations.FixedTimeEquals(credential.SecretHash, secretHash))
        {
            logger.LogWarning("Agent auth failed: invalid legacy secret for agent {AgentId} from {IP}",
                agentId, context.GetClientIp());
            context.Response.StatusCode = 401;
            return;
        }

        // Update last used
        credential.LastUsedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync();
        context.Items["AgentId"] = agentId;
        await next(context);
    }
}
