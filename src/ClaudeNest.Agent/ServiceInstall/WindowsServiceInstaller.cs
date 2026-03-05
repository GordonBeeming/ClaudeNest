using System.Diagnostics;

namespace ClaudeNest.Agent.ServiceInstall;

public sealed class WindowsServiceInstaller(ILogger logger) : IServiceInstaller
{
    private const string ServiceName = "ClaudeNestAgent";
    private const string LegacyTaskName = "ClaudeNestAgent";

    public async Task<bool> InstallAsync(string binaryPath, ServiceInstallOptions? options = null, CancellationToken ct = default)
    {
        try
        {
            // Clean up legacy scheduled task if present
            await CleanupLegacyScheduledTask(ct);

            var username = $@"{Environment.UserDomainName}\{Environment.UserName}";

            // Create the Windows Service
            var binPathArg = $"\"{binaryPath}\" run";
            var createArgs = $"create {ServiceName} binPath= \"{binPathArg}\" start= auto";

            // Add user credentials if a service password is provided
            if (!string.IsNullOrEmpty(options?.ServicePassword))
            {
                createArgs += $" obj= \"{username}\" password= \"{options.ServicePassword}\"";
            }

            var createResult = await RunCommandAsync("sc.exe", createArgs, ct);
            if (!createResult)
            {
                logger.LogError(
                    "Failed to create Windows Service. Install must be run from an elevated (Administrator) terminal");
                return false;
            }

            // Set description
            await RunCommandAsync("sc.exe",
                $"description {ServiceName} \"ClaudeNest Agent - Remote launcher for Claude Code sessions\"", ct);

            // Configure failure actions: restart after 60 seconds on first 3 failures
            await RunCommandAsync("sc.exe",
                $"failure {ServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000", ct);

            // Start the service
            var startResult = await RunCommandAsync("sc.exe", $"start {ServiceName}", ct);
            if (!startResult)
            {
                logger.LogError(
                    "Windows Service was created but failed to start. " +
                    "This is usually caused by incorrect credentials or a missing 'Log on as a service' right. " +
                    "Check Event Viewer for details");
                // Clean up the failed service
                await RunCommandAsync("sc.exe", $"delete {ServiceName}", ct);
                return false;
            }

            logger.LogInformation("Windows Service installed and started");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install Windows Service");
            return false;
        }
    }

    public async Task<bool> UninstallAsync(CancellationToken ct = default)
    {
        try
        {
            // Stop and delete the Windows Service
            await RunCommandAsync("sc.exe", $"stop {ServiceName}", ct);
            await Task.Delay(2000, ct);
            await RunCommandAsync("sc.exe", $"delete {ServiceName}", ct);

            // Also clean up legacy scheduled task if present
            await CleanupLegacyScheduledTask(ct);

            logger.LogInformation("Windows Service uninstalled");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to uninstall Windows Service");
            return false;
        }
    }

    public async Task<bool> RestartAsync(CancellationToken ct = default)
    {
        try
        {
            await RunCommandAsync("sc.exe", $"stop {ServiceName}", ct);
            await Task.Delay(2000, ct);
            await RunCommandAsync("sc.exe", $"start {ServiceName}", ct);

            logger.LogInformation("Windows Service restarted");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart Windows Service");
            return false;
        }
    }

    public async Task<bool> UpdateBinPathAsync(string newBinaryPath, CancellationToken ct = default)
    {
        try
        {
            var binPathArg = $"\"{newBinaryPath}\" run";
            var result = await RunCommandAsync("sc.exe",
                $"config {ServiceName} binPath= \"{binPathArg}\"", ct);

            if (result)
                logger.LogInformation("Windows Service binary path updated to {Path}", newBinaryPath);
            else
                logger.LogError("Failed to update Windows Service binary path");

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update Windows Service binary path");
            return false;
        }
    }

    public bool IsInstalled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"query {ServiceName}",
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

    public async Task<bool> IsRunningAsync(CancellationToken ct = default)
    {
        if (!IsInstalled()) return false;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"query {ServiceName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null) return false;
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0 && output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task CleanupLegacyScheduledTask(CancellationToken ct)
    {
        try
        {
            await RunCommandAsync("schtasks", $"/end /tn \"{LegacyTaskName}\"", ct);
            await RunCommandAsync("schtasks", $"/delete /tn \"{LegacyTaskName}\" /f", ct);
            logger.LogInformation("Cleaned up legacy scheduled task");
        }
        catch
        {
            // Ignore — legacy task may not exist
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
