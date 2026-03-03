using System.Diagnostics;

namespace ClaudeNest.Agent.ServiceInstall;

public sealed class WindowsServiceInstaller(ILogger logger) : IServiceInstaller
{
    private const string TaskName = "ClaudeNestAgent";

    public async Task<bool> InstallAsync(string binaryPath, CancellationToken ct = default)
    {
        try
        {
            var result = await RunCommandAsync("schtasks",
                $"/create /tn \"{TaskName}\" /tr \"\\\"{binaryPath}\\\" run\" /sc onlogon /rl limited /f", ct);

            if (!result)
            {
                logger.LogWarning("schtasks /create returned non-zero exit code");
                return false;
            }

            // Start the task immediately
            await RunCommandAsync("schtasks", $"/run /tn \"{TaskName}\"", ct);

            logger.LogInformation("Windows scheduled task installed and started");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install Windows scheduled task");
            return false;
        }
    }

    public async Task<bool> UninstallAsync(CancellationToken ct = default)
    {
        try
        {
            // End the task if running
            await RunCommandAsync("schtasks", $"/end /tn \"{TaskName}\"", ct);
            await RunCommandAsync("schtasks", $"/delete /tn \"{TaskName}\" /f", ct);

            logger.LogInformation("Windows scheduled task uninstalled");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to uninstall Windows scheduled task");
            return false;
        }
    }

    public async Task<bool> RestartAsync(CancellationToken ct = default)
    {
        try
        {
            await RunCommandAsync("schtasks", $"/end /tn \"{TaskName}\"", ct);
            // Brief pause to let process exit
            await Task.Delay(1000, ct);
            await RunCommandAsync("schtasks", $"/run /tn \"{TaskName}\"", ct);

            logger.LogInformation("Windows scheduled task restarted");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart Windows scheduled task");
            return false;
        }
    }

    public bool IsInstalled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/query /tn \"{TaskName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return false;

            process.WaitForExit(5000);
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
