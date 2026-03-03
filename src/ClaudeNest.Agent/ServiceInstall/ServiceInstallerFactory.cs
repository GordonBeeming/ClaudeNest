using System.Runtime.InteropServices;

namespace ClaudeNest.Agent.ServiceInstall;

public static class ServiceInstallerFactory
{
    public static IServiceInstaller Create(ILogger logger)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacOsServiceInstaller(logger);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxServiceInstaller(logger);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsServiceInstaller(logger);

        throw new PlatformNotSupportedException($"Unsupported platform: {RuntimeInformation.OSDescription}");
    }
}
