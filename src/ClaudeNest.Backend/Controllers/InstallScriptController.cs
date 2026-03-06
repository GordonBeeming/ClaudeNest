using ClaudeNest.Backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("")]
[AllowAnonymous]
[EnableRateLimiting("installScripts")]
public class InstallScriptController(IConfiguration configuration, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet("install.sh")]
    public async Task<IActionResult> GetBashScript()
    {
        var script = await LoadScriptAsync("install.sh");
        if (script is null)
            return NotFound("Install script not found");

        var backendUrl = $"{Request.Scheme}://{Request.Host}";
        var latestVersion = configuration["Agent:LatestVersion"] ?? "0.0.0";

        script = script
            .Replace("%%BACKEND_URL%%", backendUrl)
            .Replace("%%LATEST_VERSION%%", latestVersion);

        return Content(script, "text/plain");
    }

    [HttpGet("install.ps1")]
    public async Task<IActionResult> GetPowerShellScript()
    {
        var script = await LoadScriptAsync("install.ps1");
        if (script is null)
            return NotFound("Install script not found");

        var backendUrl = $"{Request.Scheme}://{Request.Host}";
        var latestVersion = configuration["Agent:LatestVersion"] ?? "0.0.0";

        script = script
            .Replace("%%BACKEND_URL%%", backendUrl)
            .Replace("%%LATEST_VERSION%%", latestVersion);

        return Content(script, "text/plain");
    }

    private async Task<string?> LoadScriptAsync(string filename)
    {
        var scriptPath = Path.Combine(env.ContentRootPath, "scripts", filename);
        if (!System.IO.File.Exists(scriptPath))
            return null;

        return await System.IO.File.ReadAllTextAsync(scriptPath);
    }
}
