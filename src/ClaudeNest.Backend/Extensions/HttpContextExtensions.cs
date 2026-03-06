namespace ClaudeNest.Backend.Extensions;

public static class HttpContextExtensions
{
    /// <summary>
    /// Gets the real client IP address. Prefers <c>CF-Connecting-IP</c> (set by Cloudflare edge,
    /// cannot be spoofed by clients) and falls back to <c>RemoteIpAddress</c> for local dev.
    /// </summary>
    public static string GetClientIp(this HttpContext context)
    {
        return context.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
    }
}
