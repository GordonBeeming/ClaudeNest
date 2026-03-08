using System.Runtime.InteropServices;
using ClaudeNest.Agent.Config;
using ClaudeNest.Agent.ServiceInstall;

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
        var claudeNestDir = GetClaudeNestDir();
        var markerPath = Path.Combine(claudeNestDir, "update-pending");
        if (!File.Exists(markerPath))
            return Task.FromResult(false);

        // Read the new version from the marker
        var newVersion = File.ReadAllText(markerPath).Trim();

        // Clean up the marker
        File.Delete(markerPath);

        // Clean up legacy updates directory if it exists
        var updatesDir = Path.Combine(claudeNestDir, "updates");
        if (Directory.Exists(updatesDir))
            Directory.Delete(updatesDir, true);

        // Clean up old versioned binaries (keep current + one previous)
        CleanupOldBinaries(newVersion);

        _logger.LogInformation("Update completed successfully to version {Version}", newVersion);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Downloads the update binary and prepares it for installation, but does NOT apply it.
    /// Returns the path to the downloaded binary.
    /// </summary>
    public async Task<string> DownloadAsync(string downloadUrl, string newVersion, CancellationToken ct = default)
    {
        var claudeNestDir = GetClaudeNestDir();
        var binDir = Path.Combine(claudeNestDir, "bin");
        Directory.CreateDirectory(binDir);

        var isWindows = OperatingSystem.IsWindows();
        var isMacOS = OperatingSystem.IsMacOS();
        var ext = isWindows ? ".exe" : "";

        // Determine the correct download URL based on RID
        var rid = GetCurrentRid();
        var filename = isWindows ? $"claudenest-agent-{rid}.exe" : $"claudenest-agent-{rid}";
        var fullUrl = downloadUrl.EndsWith('/') ? $"{downloadUrl}{filename}" : $"{downloadUrl}/{filename}";

        // Download directly to versioned path
        var versionedBinaryName = $"claudenest-agent-{newVersion}{ext}";
        var versionedBinaryPath = Path.Combine(binDir, versionedBinaryName);

        // Skip download if binary already exists (e.g. previously downloaded but deferred)
        if (File.Exists(versionedBinaryPath))
        {
            _logger.LogInformation("Update binary already exists at {Path}, skipping download", versionedBinaryPath);
            return versionedBinaryPath;
        }

        _logger.LogInformation("Downloading update from {Url} to {Path}", fullUrl, versionedBinaryPath);
        using var http = new HttpClient();
        using var response = await http.GetAsync(fullUrl, ct);
        response.EnsureSuccessStatusCode();

        await using var fs = File.Create(versionedBinaryPath);
        await response.Content.CopyToAsync(fs, ct);
        await fs.FlushAsync(ct);
        fs.Close();

        // Make executable on Unix
        if (!isWindows)
        {
            File.SetUnixFileMode(versionedBinaryPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        // Remove quarantine on macOS
        if (isMacOS)
        {
            RemoveQuarantine(versionedBinaryPath);
        }

        return versionedBinaryPath;
    }

    /// <summary>
    /// Applies a previously downloaded update: stops sessions, switches binary, and restarts.
    /// </summary>
    public async Task ApplyAsync(string versionedBinaryPath, string newVersion, Func<Task> stopSessionsAsync, CancellationToken ct = default)
    {
        // Stop sessions gracefully
        _logger.LogInformation("Stopping active sessions before update");
        await stopSessionsAsync();

        // Switch to new binary: update config, service, symlink
        await SwitchToNewBinaryAsync(versionedBinaryPath, newVersion);

        // Write update marker
        var claudeNestDir = GetClaudeNestDir();
        var markerPath = Path.Combine(claudeNestDir, "update-pending");
        await File.WriteAllTextAsync(markerPath, newVersion, ct);

        // Exit with non-zero code so service managers restart the process
        _logger.LogInformation("Update staged. Restarting agent...");
        Environment.Exit(42);
    }

    public async Task UpdateAsync(string downloadUrl, string newVersion, Func<Task> stopSessionsAsync, CancellationToken ct = default)
    {
        var binaryPath = await DownloadAsync(downloadUrl, newVersion, ct);
        await ApplyAsync(binaryPath, newVersion, stopSessionsAsync, ct);
    }

    /// <summary>
    /// Shared logic for switching to a new versioned binary. Updates config, service registration, and convenience copy.
    /// On Unix, the service always uses the stable (unversioned) path so macOS TCC permissions persist across updates.
    /// On Windows, the service uses the versioned path because the running binary can't be overwritten.
    /// </summary>
    public static async Task SwitchToNewBinaryAsync(string newVersionedPath, string? newVersion = null)
    {
        var isWindows = OperatingSystem.IsWindows();
        var ext = isWindows ? ".exe" : "";
        var binDir = Path.GetDirectoryName(newVersionedPath)!;
        var convenienceName = $"claudenest-agent{ext}";
        var conveniencePath = Path.Combine(binDir, convenienceName);

        // On Unix, the service binary is the stable (unversioned) path; on Windows it's the versioned path
        var serviceBinaryPath = isWindows ? newVersionedPath : conveniencePath;

        // Update the stable convenience binary (real copy on all platforms)
        if (!isWindows)
        {
            // Remove any existing symlink or old copy, then copy the new binary
            if (File.Exists(conveniencePath) || new FileInfo(conveniencePath).LinkTarget is not null)
            {
                File.Delete(conveniencePath);
            }
            File.Copy(newVersionedPath, conveniencePath, overwrite: true);
            File.SetUnixFileMode(conveniencePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        else
        {
            try
            {
                File.Copy(newVersionedPath, conveniencePath, overwrite: true);
            }
            catch { /* best effort — file may be in use */ }
        }

        // Check if the service binary path has actually changed before updating
        var config = ConfigLoader.LoadConfig();
        var previousBinaryPath = config.InstalledBinaryPath;

        // Update config to point to the service binary path
        config.InstalledBinaryPath = serviceBinaryPath;
        ConfigLoader.SaveConfig(config);

        // Only update service registration if the binary path changed.
        // On macOS/Linux, the service always uses the stable unversioned path, so
        // the path doesn't change between updates. Calling UpdateBinPathAsync
        // unnecessarily on macOS triggers launchctl bootout which kills the running
        // agent process before bootstrap can re-register the service.
        if (previousBinaryPath != serviceBinaryPath)
        {
            using var loggerFactory = LoggerFactory.Create(_ => { });
            var logger = loggerFactory.CreateLogger("ServiceInstaller");
            var installer = ServiceInstallerFactory.Create(logger);

            if (installer.IsInstalled())
            {
                await installer.UpdateBinPathAsync(serviceBinaryPath);
            }
        }
    }

    private void CleanupOldBinaries(string currentVersion)
    {
        try
        {
            var binDir = Path.Combine(GetClaudeNestDir(), "bin");
            if (!Directory.Exists(binDir)) return;

            var isWindows = OperatingSystem.IsWindows();
            var ext = isWindows ? ".exe" : "";
            var currentName = $"claudenest-agent-{currentVersion}{ext}";

            foreach (var file in Directory.GetFiles(binDir, $"claudenest-agent-*{ext}"))
            {
                var fileName = Path.GetFileName(file);
                // Don't delete the current version or the convenience name
                if (fileName == currentName || fileName == $"claudenest-agent{ext}")
                    continue;

                try
                {
                    File.Delete(file);
                    _logger.LogInformation("Cleaned up old binary: {FileName}", fileName);
                }
                catch
                {
                    // Best effort — may be in use on Windows
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during old binary cleanup");
        }
    }

    internal static void RemoveQuarantine(string path)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "xattr",
                Arguments = $"-d com.apple.quarantine \"{path}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(psi);
            process?.WaitForExit(5000);
        }
        catch
        {
            // Ignore — quarantine attribute may not exist
        }
    }

    private static string GetClaudeNestDir()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claudenest");
    }

    internal static string GetCurrentRid()
    {
        var arch = RuntimeInformation.ProcessArchitecture;
        if (OperatingSystem.IsWindows())
            return arch == Architecture.Arm64 ? "win-arm64" : "win-x64";
        if (OperatingSystem.IsMacOS())
            return arch == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        return arch == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
    }
}
