using System.Diagnostics;
using System.Security;

namespace ClaudeNest.Agent.ServiceInstall;

public sealed class WindowsServiceInstaller(ILogger logger) : IServiceInstaller
{
    private const string TaskName = "ClaudeNestAgent";

    public async Task<bool> InstallAsync(string binaryPath, CancellationToken ct = default)
    {
        try
        {
            var username = $@"{Environment.UserDomainName}\{Environment.UserName}";
            var xml = $"""
                <?xml version="1.0" encoding="UTF-16"?>
                <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
                  <RegistrationInfo>
                    <Description>ClaudeNest Agent - Remote launcher for Claude Code sessions</Description>
                  </RegistrationInfo>
                  <Triggers>
                    <LogonTrigger>
                      <Enabled>true</Enabled>
                      <UserId>{SecurityElement.Escape(username)}</UserId>
                    </LogonTrigger>
                  </Triggers>
                  <Principals>
                    <Principal>
                      <UserId>{SecurityElement.Escape(username)}</UserId>
                      <LogonType>InteractiveToken</LogonType>
                      <RunLevel>LeastPrivilege</RunLevel>
                    </Principal>
                  </Principals>
                  <Settings>
                    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                    <AllowStartOnDemand>true</AllowStartOnDemand>
                    <Enabled>true</Enabled>
                    <RestartOnFailure>
                      <Interval>PT1M</Interval>
                      <Count>999</Count>
                    </RestartOnFailure>
                    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                  </Settings>
                  <Actions>
                    <Exec>
                      <Command>{SecurityElement.Escape(binaryPath)}</Command>
                      <Arguments>run</Arguments>
                    </Exec>
                  </Actions>
                </Task>
                """;

            var tempFile = Path.Combine(Path.GetTempPath(), $"claudenest-task-{Guid.NewGuid():N}.xml");
            try
            {
                await File.WriteAllTextAsync(tempFile, xml, System.Text.Encoding.Unicode, ct);
                var result = await RunCommandAsync("schtasks",
                    $"/create /tn \"{TaskName}\" /xml \"{tempFile}\" /f", ct);

                if (!result)
                {
                    logger.LogError(
                        "Failed to create scheduled task. On Windows, install must be run from an elevated (Administrator) terminal");
                    return false;
                }
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }

            // Start the task immediately
            await RunCommandAsync("schtasks", $"/run /tn \"{TaskName}\"", ct);

            logger.LogInformation("Windows scheduled task installed with restart-on-failure and started");
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

    public async Task<bool> IsRunningAsync(CancellationToken ct = default)
    {
        if (!IsInstalled()) return false;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/query /tn \"{TaskName}\" /fo csv /nh",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null) return false;
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0 && output.Contains("Running", StringComparison.OrdinalIgnoreCase);
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
