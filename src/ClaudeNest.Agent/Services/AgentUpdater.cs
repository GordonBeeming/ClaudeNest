using System.Runtime.InteropServices;

namespace ClaudeNest.Agent.Services;

public sealed class AgentUpdater
{
    private readonly ILogger<AgentUpdater> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public AgentUpdater(ILogger<AgentUpdater> logger, IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _lifetime = lifetime;
    }

    public Task<bool> CheckAndCompleteUpdateAsync()
    {
        var markerPath = Path.Combine(GetClaudeNestDir(), "update-pending");
        if (!File.Exists(markerPath))
            return Task.FromResult(false);

        // Clean up
        File.Delete(markerPath);
        var updatesDir = Path.Combine(GetClaudeNestDir(), "updates");
        if (Directory.Exists(updatesDir))
            Directory.Delete(updatesDir, true);

        _logger.LogInformation("Update completed successfully");
        return Task.FromResult(true);
    }

    public async Task UpdateAsync(string downloadUrl, string newVersion, Func<Task> stopSessionsAsync, CancellationToken ct = default)
    {
        var claudeNestDir = GetClaudeNestDir();
        var updatesDir = Path.Combine(claudeNestDir, "updates");
        Directory.CreateDirectory(updatesDir);

        var isWindows = OperatingSystem.IsWindows();
        var newBinaryName = isWindows ? "claudenest-agent.new.exe" : "claudenest-agent.new";
        var newBinaryPath = Path.Combine(updatesDir, newBinaryName);

        // Determine the correct download URL based on RID
        var rid = GetCurrentRid();
        var filename = isWindows ? $"claudenest-agent-{rid}.exe" : $"claudenest-agent-{rid}";
        var fullUrl = downloadUrl.EndsWith('/') ? $"{downloadUrl}{filename}" : $"{downloadUrl}/{filename}";

        // Download
        _logger.LogInformation("Downloading update from {Url}", fullUrl);
        using var http = new HttpClient();
        using var response = await http.GetAsync(fullUrl, ct);
        response.EnsureSuccessStatusCode();

        await using var fs = File.Create(newBinaryPath);
        await response.Content.CopyToAsync(fs, ct);
        await fs.FlushAsync(ct);

        // Make executable on Unix
        if (!isWindows)
        {
            File.SetUnixFileMode(newBinaryPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        // Stop sessions gracefully
        _logger.LogInformation("Stopping active sessions before update");
        await stopSessionsAsync();

        // Replace binary
        var installedBinaryPath = Path.Combine(claudeNestDir, "bin",
            isWindows ? "claudenest-agent.exe" : "claudenest-agent");

        if (File.Exists(installedBinaryPath))
        {
            if (isWindows)
            {
                // On Windows, write a batch script to replace after exit
                var scriptPath = Path.Combine(updatesDir, "update.bat");
                var script = $"""
                    @echo off
                    timeout /t 2 /nobreak >nul
                    copy /y "{newBinaryPath}" "{installedBinaryPath}"
                    del "{scriptPath}"
                    """;
                File.WriteAllText(scriptPath, script);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{scriptPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            else
            {
                // On Unix, we can replace the running binary via rename
                File.Move(newBinaryPath, installedBinaryPath, overwrite: true);
            }
        }

        // Write update marker
        var markerPath = Path.Combine(claudeNestDir, "update-pending");
        await File.WriteAllTextAsync(markerPath, newVersion, ct);

        // Exit with non-zero code so service managers restart the process
        // (launchd with SuccessfulExit=false won't restart on exit code 0)
        _logger.LogInformation("Update staged. Restarting agent...");
        Environment.Exit(42);
    }

    private static string GetClaudeNestDir()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claudenest");
    }

    private static string GetCurrentRid()
    {
        var arch = RuntimeInformation.ProcessArchitecture;
        if (OperatingSystem.IsWindows())
            return arch == Architecture.Arm64 ? "win-arm64" : "win-x64";
        if (OperatingSystem.IsMacOS())
            return arch == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        return arch == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
    }
}
