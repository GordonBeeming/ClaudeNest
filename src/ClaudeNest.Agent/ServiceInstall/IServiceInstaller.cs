namespace ClaudeNest.Agent.ServiceInstall;

public interface IServiceInstaller
{
    Task<bool> InstallAsync(string binaryPath, CancellationToken ct = default);
    Task<bool> UninstallAsync(CancellationToken ct = default);
    Task<bool> RestartAsync(CancellationToken ct = default);
    bool IsInstalled();
    Task<bool> IsRunningAsync(CancellationToken ct = default);
}
