using System.Diagnostics;

namespace ClaudeNest.Agent.ServiceInstall;

public sealed class MacOsServiceInstaller(ILogger logger) : IServiceInstaller
{
    private const string Label = "com.claudenest.agent";

    private static string PlistPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", $"{Label}.plist");

    private static string LogDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claudenest", "logs");

    public async Task<bool> InstallAsync(string binaryPath, ServiceInstallOptions? options = null, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PlistPath)!);
            Directory.CreateDirectory(LogDir);

            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var stdoutLog = Path.Combine(LogDir, "agent.log");
            var stderrLog = Path.Combine(LogDir, "agent-error.log");

            var plist = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <key>Label</key>
                    <string>{Label}</string>
                    <key>ProgramArguments</key>
                    <array>
                        <string>{binaryPath}</string>
                        <string>run</string>
                    </array>
                    <key>RunAtLoad</key>
                    <true/>
                    <key>KeepAlive</key>
                    <true/>
                    <key>ThrottleInterval</key>
                    <integer>10</integer>
                    <key>StandardOutPath</key>
                    <string>{stdoutLog}</string>
                    <key>StandardErrorPath</key>
                    <string>{stderrLog}</string>
                    <key>WorkingDirectory</key>
                    <string>{homeDir}</string>
                </dict>
                </plist>
                """;

            await File.WriteAllTextAsync(PlistPath, plist, ct);
            logger.LogInformation("Wrote LaunchAgent plist to {Path}", PlistPath);

            var loadResult = await RunCommandAsync("launchctl", $"load \"{PlistPath}\"", ct);
            if (!loadResult)
            {
                logger.LogWarning("launchctl load returned non-zero exit code");
            }

            logger.LogInformation("macOS LaunchAgent installed and loaded");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install macOS LaunchAgent");
            return false;
        }
    }

    public async Task<bool> UninstallAsync(CancellationToken ct = default)
    {
        try
        {
            if (File.Exists(PlistPath))
            {
                await RunCommandAsync("launchctl", $"unload \"{PlistPath}\"", ct);
                File.Delete(PlistPath);
                logger.LogInformation("macOS LaunchAgent uninstalled");
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to uninstall macOS LaunchAgent");
            return false;
        }
    }

    public async Task<bool> RestartAsync(CancellationToken ct = default)
    {
        try
        {
            await RunCommandAsync("launchctl", $"unload \"{PlistPath}\"", ct);
            await RunCommandAsync("launchctl", $"load \"{PlistPath}\"", ct);
            logger.LogInformation("macOS LaunchAgent restarted");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart macOS LaunchAgent");
            return false;
        }
    }

    public async Task<bool> UpdateBinPathAsync(string newBinaryPath, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(PlistPath))
            {
                logger.LogWarning("Cannot update binary path — plist does not exist");
                return false;
            }

            // Unload, rewrite plist with new path, reload
            await RunCommandAsync("launchctl", $"unload \"{PlistPath}\"", ct);

            // Re-install with new binary path (rewrites the plist)
            await InstallAsync(newBinaryPath, ct: ct);

            logger.LogInformation("macOS LaunchAgent binary path updated to {Path}", newBinaryPath);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update macOS LaunchAgent binary path");
            return false;
        }
    }

    public bool IsInstalled() => File.Exists(PlistPath);

    public async Task<bool> IsRunningAsync(CancellationToken ct = default)
    {
        if (!IsInstalled()) return false;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "launchctl",
                Arguments = $"list {Label}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null) return false;
            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> RunCommandAsync(string fileName, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null) return false;

        await process.WaitForExitAsync(ct);
        return process.ExitCode == 0;
    }
}
