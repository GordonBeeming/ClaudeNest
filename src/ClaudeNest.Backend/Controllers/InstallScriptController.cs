using System.Security.Cryptography;
using System.Text;
using ClaudeNest.Backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("")]
[AllowAnonymous]
public class InstallScriptController(NestDbContext db, TimeProvider timeProvider, IConfiguration configuration, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet("install.sh")]
    public async Task<IActionResult> GetBashScript([FromQuery] string? token)
    {
        if (string.IsNullOrEmpty(token))
            return BadRequest("Token is required");

        var isValid = await ValidateTokenAsync(token);
        if (!isValid)
            return BadRequest("Invalid or expired token");

        var script = await LoadScriptAsync("install.sh");
        if (script is null)
            return NotFound("Install script not found");

        var backendUrl = $"{Request.Scheme}://{Request.Host}";
        var latestVersion = configuration["Agent:LatestVersion"] ?? "0.0.0";

        script = script
            .Replace("%%TOKEN%%", token)
            .Replace("%%BACKEND_URL%%", backendUrl)
            .Replace("%%LATEST_VERSION%%", latestVersion);

        return Content(script, "text/plain");
    }

    [HttpGet("install.ps1")]
    public async Task<IActionResult> GetPowerShellScript([FromQuery] string? token)
    {
        if (string.IsNullOrEmpty(token))
            return BadRequest("Token is required");

        var isValid = await ValidateTokenAsync(token);
        if (!isValid)
            return BadRequest("Invalid or expired token");

        var script = await LoadScriptAsync("install.ps1");
        if (script is null)
            return NotFound("Install script not found");

        var backendUrl = $"{Request.Scheme}://{Request.Host}";
        var latestVersion = configuration["Agent:LatestVersion"] ?? "0.0.0";

        script = script
            .Replace("%%TOKEN%%", token)
            .Replace("%%BACKEND_URL%%", backendUrl)
            .Replace("%%LATEST_VERSION%%", latestVersion);

        return Content(script, "text/plain");
    }

    private async Task<bool> ValidateTokenAsync(string token)
    {
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var now = timeProvider.GetUtcNow();

        // Check if the token exists and is not expired (do NOT burn it — scripts may be re-downloaded)
        return await db.PairingTokens
            .AnyAsync(t => t.TokenHash == tokenHash && t.RedeemedAt == null && t.ExpiresAt > now);
    }

    private async Task<string?> LoadScriptAsync(string filename)
    {
        var scriptPath = Path.Combine(env.ContentRootPath, "scripts", filename);
        if (!System.IO.File.Exists(scriptPath))
            return null;

        return await System.IO.File.ReadAllTextAsync(scriptPath);
    }
}
