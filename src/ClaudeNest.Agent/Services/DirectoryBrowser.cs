using ClaudeNest.Agent.Config;

namespace ClaudeNest.Agent.Services;

public sealed class DirectoryBrowser(NestConfig config)
{
    public List<string> List(string path)
    {
        var resolved = Path.GetFullPath(path);

        if (!config.AllowedPaths.Any(a => resolved.StartsWith(a, StringComparison.OrdinalIgnoreCase)))
            return [];

        if (config.DeniedPaths.Any(d => resolved.StartsWith(d, StringComparison.OrdinalIgnoreCase)))
            return [];

        if (!Directory.Exists(resolved))
            return [];

        return Directory.GetDirectories(resolved)
            .Where(dir => !config.DeniedPaths.Any(d => dir.StartsWith(d, StringComparison.OrdinalIgnoreCase)))
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }

    public bool IsPathAllowed(string path)
    {
        var resolved = Path.GetFullPath(path);

        if (!config.AllowedPaths.Any(a => resolved.StartsWith(a, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (config.DeniedPaths.Any(d => resolved.StartsWith(d, StringComparison.OrdinalIgnoreCase)))
            return false;

        return Directory.Exists(resolved);
    }
}
