using System.Diagnostics;

namespace ClaudeNest.Agent.ServiceInstall;

public sealed class MacOsServiceInstaller(ILogger logger) : IServiceInstaller
{
    private const string Label = "app.claudenest.agent";
    private const string OldLabel = "com.claudenest.agent";

    private static string PlistPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", $"{Label}.plist");

    private static string OldPlistPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", $"{OldLabel}.plist");

    private static string LogDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claudenest", "logs");

    private static string GuiDomain => $"gui/{GetUid()}";

    private static string ServiceTarget => $"{GuiDomain}/{Label}";

    private static string OldServiceTarget => $"{GuiDomain}/{OldLabel}";

    public async Task<bool> InstallAsync(string binaryPath, ServiceInstallOptions? options = null, CancellationToken ct = default)
    {
        try
        {
            // Migrate from old label (com.claudenest.agent) if it exists
            await MigrateFromOldLabelAsync(ct);

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

            // Use modern launchctl bootstrap API (persists across reboots, unlike legacy 'load')
            var bootstrapResult = await RunCommandAsync("launchctl", $"bootstrap {GuiDomain} \"{PlistPath}\"", ct);
            if (!bootstrapResult)
            {
                logger.LogWarning("launchctl bootstrap returned non-zero exit code");
            }

            logger.LogInformation("macOS LaunchAgent installed and bootstrapped");
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
            // Bootout the service (stops and unregisters it)
            await RunCommandAsync("launchctl", $"bootout {ServiceTarget}", ct);

            if (File.Exists(PlistPath))
            {
                File.Delete(PlistPath);
            }

            logger.LogInformation("macOS LaunchAgent uninstalled");
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
            // kickstart -k forces a restart of the service
            await RunCommandAsync("launchctl", $"kickstart -k {ServiceTarget}", ct);
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

            // Bootout the current service, rewrite plist, bootstrap with new path
            await RunCommandAsync("launchctl", $"bootout {ServiceTarget}", ct);

            // Re-install with new binary path (rewrites the plist and bootstraps)
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

    public bool IsInstalled() => File.Exists(PlistPath) || File.Exists(OldPlistPath);

    public async Task<bool> IsRunningAsync(CancellationToken ct = default)
    {
        if (!IsInstalled()) return false;
        try
        {
            // Check both old and new service targets
            foreach (var target in new[] { ServiceTarget, OldServiceTarget })
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "launchctl",
                    Arguments = $"print {target}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                if (process is null) continue;
                var output = await process.StandardOutput.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);
                if (process.ExitCode == 0 && output.Contains("pid = ", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task MigrateFromOldLabelAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(OldPlistPath)) return;

            logger.LogInformation("Migrating LaunchAgent from old label {OldLabel} to {NewLabel}", OldLabel, Label);

            // Bootout old service (ignore errors if not registered)
            await RunCommandAsync("launchctl", $"bootout {OldServiceTarget}", ct);

            File.Delete(OldPlistPath);
            logger.LogInformation("Removed old LaunchAgent plist at {Path}", OldPlistPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error migrating from old LaunchAgent label");
        }
    }

    private static uint GetUid()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "id",
                Arguments = "-u",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null) return 501; // fallback to common default
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return uint.TryParse(output, out var uid) ? uid : 501;
        }
        catch
        {
            return 501;
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
