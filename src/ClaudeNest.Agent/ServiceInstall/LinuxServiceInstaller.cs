using System.Diagnostics;

namespace ClaudeNest.Agent.ServiceInstall;

public sealed class LinuxServiceInstaller(ILogger logger) : IServiceInstaller
{
    private const string ServiceName = "claudenest-agent";

    private static string ServiceFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "systemd", "user", $"{ServiceName}.service");

    public async Task<bool> InstallAsync(string binaryPath, ServiceInstallOptions? options = null, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ServiceFilePath)!);

            var serviceContent = $"""
                [Unit]
                Description=ClaudeNest Agent
                After=network-online.target
                Wants=network-online.target

                [Service]
                Type=simple
                ExecStart={binaryPath} run
                Restart=always
                RestartSec=10
                Environment=DOTNET_ROOT=%h/.dotnet

                [Install]
                WantedBy=default.target
                """;

            await File.WriteAllTextAsync(ServiceFilePath, serviceContent, ct);
            logger.LogInformation("Wrote systemd user service to {Path}", ServiceFilePath);

            await RunCommandAsync("systemctl", "--user daemon-reload", ct);
            await RunCommandAsync("systemctl", $"--user enable {ServiceName}", ct);
            await RunCommandAsync("systemctl", $"--user start {ServiceName}", ct);
            await RunCommandAsync("loginctl", "enable-linger", ct);

            logger.LogInformation("Linux systemd user service installed and started");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install Linux systemd service");
            return false;
        }
    }

    public async Task<bool> UninstallAsync(CancellationToken ct = default)
    {
        try
        {
            await RunCommandAsync("systemctl", $"--user stop {ServiceName}", ct);
            await RunCommandAsync("systemctl", $"--user disable {ServiceName}", ct);

            if (File.Exists(ServiceFilePath))
            {
                File.Delete(ServiceFilePath);
            }

            await RunCommandAsync("systemctl", "--user daemon-reload", ct);

            logger.LogInformation("Linux systemd user service uninstalled");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to uninstall Linux systemd service");
            return false;
        }
    }

    public async Task<bool> RestartAsync(CancellationToken ct = default)
    {
        try
        {
            await RunCommandAsync("systemctl", $"--user restart {ServiceName}", ct);
            logger.LogInformation("Linux systemd user service restarted");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart Linux systemd service");
            return false;
        }
    }

    public async Task<bool> UpdateBinPathAsync(string newBinaryPath, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(ServiceFilePath))
            {
                logger.LogWarning("Cannot update binary path — service file does not exist");
                return false;
            }

            // Rewrite service file with new binary path
            var serviceContent = $"""
                [Unit]
                Description=ClaudeNest Agent
                After=network-online.target
                Wants=network-online.target

                [Service]
                Type=simple
                ExecStart={newBinaryPath} run
                Restart=always
                RestartSec=10
                Environment=DOTNET_ROOT=%h/.dotnet

                [Install]
                WantedBy=default.target
                """;

            await File.WriteAllTextAsync(ServiceFilePath, serviceContent, ct);
            await RunCommandAsync("systemctl", "--user daemon-reload", ct);

            logger.LogInformation("Linux systemd service binary path updated to {Path}", newBinaryPath);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update Linux systemd service binary path");
            return false;
        }
    }

    public bool IsInstalled() => File.Exists(ServiceFilePath);

    public async Task<bool> IsRunningAsync(CancellationToken ct = default)
    {
        if (!IsInstalled()) return false;
        return await RunCommandAsync("systemctl", $"--user is-active {ServiceName}", ct);
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
