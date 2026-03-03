using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace ClaudeNest.Backend.Controllers;

[ApiController]
[Route("api/agent-download")]
[Authorize]
public class AgentDownloadController(IConfiguration configuration, IWebHostEnvironment environment, ILogger<AgentDownloadController> logger) : ControllerBase
{
    private static readonly string[] AllowedRids = ["win-x64", "osx-arm64", "osx-x64", "linux-x64"];

    [HttpGet("available")]
    public IActionResult GetAvailability()
    {
        var version = configuration["Agent:LatestVersion"] ?? "1.0.0";

        if (!IsEnabled())
        {
            // In production, return GitHub Releases info
            return Ok(new { available = true, rids = AllowedRids, version, source = "github-releases" });
        }

        // In dev, the workspace is at <repo-root>/.dev-workspace. ContentRoot = src/ClaudeNest.Backend/
        var devWorkspacePath = configuration["DevWorkspacePath"]
            ?? Environment.GetEnvironmentVariable("DevWorkspacePath");

        // Fallback: compute from content root if not explicitly set
        if (string.IsNullOrEmpty(devWorkspacePath) && environment.IsDevelopment())
        {
            var candidate = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", ".dev-workspace"));
            if (Directory.Exists(candidate))
                devWorkspacePath = candidate;
        }

        return Ok(new { available = true, rids = AllowedRids, devWorkspacePath, version, source = "local-build" });
    }

    [HttpGet("{rid}")]
    public async Task<IActionResult> DownloadAgent(string rid, CancellationToken cancellationToken)
    {
        if (!AllowedRids.Contains(rid))
            return BadRequest($"Invalid RID '{rid}'. Allowed: {string.Join(", ", AllowedRids)}");

        // In production (or when local build is disabled), redirect to GitHub Releases
        if (!IsEnabled())
        {
            var version = configuration["Agent:LatestVersion"] ?? "1.0.0";
            var filename = rid.StartsWith("win-") ? $"claudenest-agent-{rid}.exe" : $"claudenest-agent-{rid}";
            return Redirect($"https://github.com/gordonbeeming/ClaudeNest/releases/download/agent-v{version}/{filename}");
        }

        var projectPath = ResolveProjectPath();
        if (projectPath is null || !System.IO.File.Exists(projectPath))
        {
            logger.LogError("Agent project not found at {ProjectPath}", projectPath);
            return StatusCode(500, "Agent project path not configured or not found");
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"claudenest-build-{rid}-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);

            var args = $"publish \"{projectPath}\" -r {rid} -c Release --self-contained true -p:PublishAot=false -p:PublishSingleFile=true -o \"{tempDir}\"";

            logger.LogInformation("Building agent for {Rid}: dotnet {Args}", rid, args);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
                return StatusCode(500, "Failed to start dotnet publish");

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                logger.LogError("dotnet publish failed (exit {ExitCode}): {StdErr}", process.ExitCode, stderr);
                return StatusCode(500, $"Build failed (exit code {process.ExitCode})");
            }

            // Find the output binary
            var isWindows = rid.StartsWith("win-");
            var binaryName = isWindows ? "ClaudeNest.Agent.exe" : "ClaudeNest.Agent";
            var binaryPath = Path.Combine(tempDir, binaryName);

            if (!System.IO.File.Exists(binaryPath))
            {
                logger.LogError("Expected binary not found at {BinaryPath}", binaryPath);
                return StatusCode(500, "Build succeeded but output binary not found");
            }

            var downloadFilename = isWindows ? $"claudenest-agent-{rid}.exe" : $"claudenest-agent-{rid}";

            var stream = new FileStream(binaryPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Clean up temp dir after response completes
            Response.OnCompleted(() =>
            {
                try { Directory.Delete(tempDir, true); }
                catch { /* best effort */ }
                return Task.CompletedTask;
            });

            return File(stream, "application/octet-stream", downloadFilename);
        }
        catch (OperationCanceledException)
        {
            CleanupTempDir(tempDir);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error building agent for {Rid}", rid);
            CleanupTempDir(tempDir);
            return StatusCode(500, "Unexpected error during build");
        }
    }

    private bool IsEnabled()
    {
        return string.Equals(configuration["LocalAgentBuild:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
    }

    private string? ResolveProjectPath()
    {
        var relative = configuration["LocalAgentBuild:AgentProjectPath"];
        if (string.IsNullOrEmpty(relative))
            return null;

        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, relative));
    }

    private static void CleanupTempDir(string path)
    {
        try { Directory.Delete(path, true); }
        catch { /* best effort */ }
    }
}
