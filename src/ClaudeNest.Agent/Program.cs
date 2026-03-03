using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using ClaudeNest.Agent;
using ClaudeNest.Agent.Config;
using ClaudeNest.Agent.Serialization;
using ClaudeNest.Agent.ServiceInstall;

// Handle '--add-path' flag: add directories to an existing agent
if (args.Any(a => a == "--add-path"))
{
    return await HandleAddPathCommand(args);
}

// Handle 'uninstall' subcommand
if (args.Length > 0 && args[0] == "uninstall")
{
    return await HandleUninstallAsync();
}

// Handle 'install' subcommand: pair, register service, and optionally run interactively
if (args.Length > 0 && args[0] == "install")
{
    var exitCode = await HandleInstallAsync(args);
    if (exitCode >= 0)
        return exitCode; // 0 = service installed and running, >0 = error

    // exitCode == -1 means service registration failed, fall through to run in foreground
    Console.WriteLine("Starting agent in foreground...");
    Console.WriteLine("Press Ctrl+C to stop.");
    Console.WriteLine();
}

// Run the agent (works for both post-install and standalone 'run')
var hostArgs = args.Length > 0 && (args[0] == "install" || args[0] == "run")
    ? args[1..] // strip subcommand so the host doesn't choke on it
    : args;

var builder = Host.CreateApplicationBuilder(hostArgs);

builder.AddServiceDefaults();

builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();
host.Run();
return 0;

static async Task<int> HandleInstallAsync(string[] args)
{
    string? token = null;
    string? backendUrl = null;
    string? agentName = null;
    List<string> paths = [];

    for (var i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--token" when i + 1 < args.Length:
                token = args[++i];
                break;
            case "--backend" when i + 1 < args.Length:
                backendUrl = args[++i];
                break;
            case "--name" when i + 1 < args.Length:
                agentName = args[++i];
                break;
            case "--path" when i + 1 < args.Length:
                paths.Add(Path.GetFullPath(args[++i]));
                break;
        }
    }

    // Check if agent is already installed on this machine
    var existingCredentials = ConfigLoader.LoadCredentials();
    var existingConfig = ConfigLoader.LoadConfig();
    var isExistingInstall = existingCredentials is not null && existingConfig.AllowedPaths.Count > 0;

    if (isExistingInstall)
    {
        // Existing agent — add paths to current config
        return await HandleAddPathsAsync(existingCredentials!, existingConfig, paths);
    }

    // New install — require token and backend
    if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(backendUrl))
    {
        Console.Error.WriteLine("Usage: claudenest-agent install --token <TOKEN> --backend <URL> [--name <NAME>] [--path <PATH> ...]");
        return 1;
    }

    // Prompt for agent name if not provided
    if (string.IsNullOrEmpty(agentName))
    {
        var defaultName = Environment.MachineName;
        Console.Write($"Agent name [{defaultName}]: ");
        var input = Console.ReadLine()?.Trim();
        agentName = string.IsNullOrEmpty(input) ? defaultName : input;
    }

    // Prompt for allowed paths if not provided
    if (paths.Count == 0)
    {
        var defaultPath = Directory.GetCurrentDirectory();
        Console.Write($"Allowed path [{defaultPath}]: ");
        var input = Console.ReadLine()?.Trim();
        var path = string.IsNullOrEmpty(input) ? defaultPath : Path.GetFullPath(input);

        if (!Directory.Exists(path))
        {
            Console.Error.WriteLine($"Directory does not exist: {path}");
            return 1;
        }

        paths.Add(path);
    }

    // Validate and trust paths
    var trustResult = await ValidateAndTrustPaths(paths, backendUrl, agentName);
    if (trustResult != 0) return trustResult;

    Console.WriteLine();
    Console.WriteLine($"Pairing with backend at {backendUrl}...");

    var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
        : "linux";

    var request = new PairingExchangeRequest
    {
        Token = token,
        AgentName = agentName,
        Hostname = Environment.MachineName,
        OS = os
    };

    using var http = new HttpClient();
    var content = new StringContent(
        JsonSerializer.Serialize(request, AgentJsonContext.Default.PairingExchangeRequest),
        System.Text.Encoding.UTF8,
        "application/json");

    HttpResponseMessage response;
    try
    {
        response = await http.PostAsync($"{backendUrl.TrimEnd('/')}/api/pairing/exchange", content);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to connect to backend: {ex.Message}");
        return 1;
    }

    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        Console.Error.WriteLine($"Pairing failed ({response.StatusCode}): {error}");
        return 1;
    }

    var result = await response.Content.ReadFromJsonAsync(AgentJsonContext.Default.PairingExchangeResponse);
    if (result is null)
    {
        Console.Error.WriteLine("Pairing failed: empty response from backend");
        return 1;
    }

    // Save credentials
    var credentials = new AgentCredentials
    {
        AgentId = result.AgentId,
        Secret = result.Secret,
        BackendUrl = backendUrl
    };
    ConfigLoader.SaveCredentials(credentials);

    return await InstallBinaryAndService(credentials, agentName, paths);
}

static async Task<int> HandleAddPathsAsync(AgentCredentials credentials, NestConfig existingConfig, List<string> newPaths)
{
    Console.WriteLine($"Existing agent found: {existingConfig.Name} (ID: {credentials.AgentId})");
    Console.WriteLine($"Current paths: {string.Join(", ", existingConfig.AllowedPaths)}");
    Console.WriteLine();

    // Prompt for new path if not provided via --path
    if (newPaths.Count == 0)
    {
        var defaultPath = Directory.GetCurrentDirectory();
        Console.Write($"Path to add [{defaultPath}]: ");
        var input = Console.ReadLine()?.Trim();
        var path = string.IsNullOrEmpty(input) ? defaultPath : Path.GetFullPath(input);

        if (!Directory.Exists(path))
        {
            Console.Error.WriteLine($"Directory does not exist: {path}");
            return 1;
        }

        newPaths.Add(path);
    }

    // Filter out paths that are already configured
    var pathsToAdd = newPaths
        .Where(p => !existingConfig.AllowedPaths.Any(existing =>
            string.Equals(Path.GetFullPath(existing), Path.GetFullPath(p), StringComparison.OrdinalIgnoreCase)))
        .ToList();

    if (pathsToAdd.Count == 0)
    {
        Console.WriteLine("All specified paths are already configured. Nothing to do.");
        return 0;
    }

    // Validate and trust the new paths
    var trustResult = await ValidateAndTrustPaths(pathsToAdd, credentials.BackendUrl, existingConfig.Name ?? Environment.MachineName);
    if (trustResult != 0) return trustResult;

    // Merge paths into existing config
    var mergedPaths = existingConfig.AllowedPaths.Concat(pathsToAdd).Distinct().ToList();
    existingConfig.AllowedPaths = mergedPaths;
    ConfigLoader.SaveConfig(existingConfig);

    Console.WriteLine();
    Console.WriteLine("Configuration updated:");
    Console.WriteLine($"  Agent ID:    {credentials.AgentId}");
    Console.WriteLine($"  Name:        {existingConfig.Name}");
    Console.WriteLine($"  Paths:       {string.Join(", ", mergedPaths)}");
    Console.WriteLine($"  Added:       {string.Join(", ", pathsToAdd)}");

    // Restart the service so it picks up the new config
    Console.WriteLine();
    Console.WriteLine("Restarting agent service...");
    using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
    var serviceLogger = loggerFactory.CreateLogger("ServiceInstaller");

    try
    {
        var installer = ServiceInstallerFactory.Create(serviceLogger);
        if (installer.IsInstalled())
        {
            await installer.UninstallAsync();
            var binaryPath = existingConfig.InstalledBinaryPath
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claudenest", "bin",
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "claudenest-agent.exe" : "claudenest-agent");

            // Update binary if running from a different location
            var currentBinaryPath = Environment.ProcessPath;
            if (currentBinaryPath is not null && currentBinaryPath != binaryPath && File.Exists(currentBinaryPath))
            {
                Console.WriteLine($"Updating agent binary at {binaryPath}...");
                File.Copy(currentBinaryPath, binaryPath, overwrite: true);
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    File.SetUnixFileMode(binaryPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
            }

            await installer.InstallAsync(binaryPath);
            Console.WriteLine("Agent restarted. New paths are now active.");
        }
        else
        {
            Console.WriteLine("No background service found. Start the agent manually or re-run a full install.");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Warning: Could not restart service ({ex.Message}). Restart the agent manually for changes to take effect.");
    }

    return 0;
}

static async Task<int> ValidateAndTrustPaths(List<string> paths, string backendUrl, string agentName)
{
    // Validate all paths exist
    foreach (var path in paths)
    {
        if (!Directory.Exists(path))
        {
            Console.Error.WriteLine($"Directory does not exist: {path}");
            return 1;
        }
    }

    // Check workspace trust for each allowed path
    Console.WriteLine();
    Console.WriteLine("🔍 Checking workspace trust...");
    var untrustedDirs = new List<string>();
    foreach (var allowedPath in paths)
    {
        Console.Write($"   Checking {allowedPath}... ");
        if (await IsWorkspaceUntrustedAsync(allowedPath))
        {
            Console.WriteLine("❌ not trusted");
            untrustedDirs.Add(allowedPath);
        }
        else
        {
            Console.WriteLine("✅ trusted");
        }
    }

    if (untrustedDirs.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("⚠️  Some directories need to be trusted by Claude Code before the agent can use them.");
        Console.WriteLine("   We'll open Claude in each directory so you can accept the trust prompt.");
        Console.WriteLine();
        Console.Write("👉 Press Enter to continue...");
        Console.ReadLine();

        foreach (var dir in untrustedDirs)
        {
            Console.WriteLine();
            Console.WriteLine($"📂 Trusting: {dir}");
            Console.WriteLine("   Claude will open — accept the trust dialog, then type /exit to close Claude.");
            Console.WriteLine();

            await RunClaudeTrustAsync(dir);
        }

        // Re-check trust after user interaction
        Console.WriteLine();
        Console.WriteLine("🔍 Verifying trust...");
        var stillUntrusted = new List<string>();
        foreach (var dir in untrustedDirs)
        {
            Console.Write($"   Checking {dir}... ");
            if (await IsWorkspaceUntrustedAsync(dir))
            {
                Console.WriteLine("❌ still not trusted");
                stillUntrusted.Add(dir);
            }
            else
            {
                Console.WriteLine("✅ trusted");
            }
        }

        if (stillUntrusted.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("❌ The following directories are still not trusted by Claude Code:");
            foreach (var dir in stillUntrusted)
                Console.Error.WriteLine($"   - {dir}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("You can manually trust them by running 'claude' in each directory:");
            foreach (var dir in stillUntrusted)
                Console.Error.WriteLine($"   cd \"{dir}\" && claude");
            Console.Error.WriteLine();

            var pathArgs = string.Join(" ", paths.Select(p => $"--path \"{p}\""));
            Console.Error.WriteLine("Then re-run the install command:");
            Console.Error.WriteLine($"   ./{Path.GetFileName(Environment.ProcessPath ?? "claudenest-agent")} install --token <NEW_TOKEN> --backend {backendUrl} --name \"{agentName}\" {pathArgs}");
            return 1;
        }
    }

    Console.WriteLine();
    Console.WriteLine("✅ All workspaces trusted.");
    return 0;
}

static async Task<int> InstallBinaryAndService(AgentCredentials credentials, string agentName, List<string> paths)
{
    // Copy binary to ~/.claudenest/bin/
    var claudeNestDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claudenest");
    var binDir = Path.Combine(claudeNestDir, "bin");
    Directory.CreateDirectory(binDir);

    var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    var binaryName = isWindows ? "claudenest-agent.exe" : "claudenest-agent";
    var installedBinaryPath = Path.Combine(binDir, binaryName);
    var currentBinaryPath = Environment.ProcessPath;

    if (currentBinaryPath is not null && currentBinaryPath != installedBinaryPath)
    {
        Console.WriteLine($"Copying agent binary to {installedBinaryPath}...");
        File.Copy(currentBinaryPath, installedBinaryPath, overwrite: true);

        // Make executable on Unix
        if (!isWindows)
        {
            File.SetUnixFileMode(installedBinaryPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    // Add ~/.claudenest/bin to PATH
    EnsureBinOnPath(binDir);

    // Determine service type
    var serviceType = isWindows ? "windows-task" :
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos-launchagent" :
        "linux-systemd";

    // Save config with name, allowed paths, and service info
    var nestConfig = new NestConfig
    {
        Name = agentName,
        AllowedPaths = paths,
        InstalledBinaryPath = installedBinaryPath,
        ServiceType = serviceType
    };
    ConfigLoader.SaveConfig(nestConfig);

    // Register and start as a service
    Console.WriteLine("Registering as a background service...");
    using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
    var serviceLogger = loggerFactory.CreateLogger("ServiceInstaller");

    try
    {
        var installer = ServiceInstallerFactory.Create(serviceLogger);
        var installResult = await installer.InstallAsync(installedBinaryPath);
        if (installResult)
        {
            Console.WriteLine();
            Console.WriteLine($"Agent paired and installed successfully!");
            Console.WriteLine($"  Agent ID:    {credentials.AgentId}");
            Console.WriteLine($"  Name:        {agentName}");
            Console.WriteLine($"  Binary:      {installedBinaryPath}");
            Console.WriteLine($"  Service:     {serviceType}");
            Console.WriteLine($"  Paths:       {string.Join(", ", paths)}");
            Console.WriteLine($"  Config:      {claudeNestDir}/");
            Console.WriteLine();
            Console.WriteLine("The agent is now running as a background service and will start automatically on login.");
            Console.WriteLine("To uninstall, run: claudenest-agent uninstall");
            // Exit without running the agent inline since the service is now managing it
            return 0;
        }
        else
        {
            Console.WriteLine("Warning: Service registration failed. The agent will run in the foreground.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not register as a service ({ex.Message}). The agent will run in the foreground.");
    }

    Console.WriteLine($"Agent paired successfully! Agent ID: {credentials.AgentId}");
    Console.WriteLine($"Name: {agentName}");
    Console.WriteLine($"Allowed paths: {string.Join(", ", paths)}");
    Console.WriteLine("Configuration saved to ~/.claudenest/");
    // Return -1 to signal: pairing succeeded but service registration failed, run in foreground
    return -1;
}

static async Task RunClaudeTrustAsync(string directoryPath)
{
    try
    {
        // Run claude interactively so the user sees and can accept the trust dialog.
        // We inherit stdin/stdout/stderr so the trust prompt appears in the terminal.
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "claude",
                WorkingDirectory = directoryPath,
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = false
            }
        };

        process.Start();

        // Wait for the user to accept trust and then exit claude (or timeout after 2 min)
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("   ⚠️  Timed out waiting for Claude to finish.");
            try { process.Kill(entireProcessTree: true); } catch { }
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"   ⚠️  Could not run claude: {ex.Message}");
    }
}

static async Task<int> HandleAddPathCommand(string[] args)
{
    List<string> paths = [];
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--add-path" && i + 1 < args.Length)
        {
            paths.Add(Path.GetFullPath(args[++i]));
        }
    }

    var credentials = ConfigLoader.LoadCredentials();
    var config = ConfigLoader.LoadConfig();

    if (credentials is null || config.AllowedPaths.Count == 0)
    {
        Console.Error.WriteLine("No agent installed. Run 'claudenest-agent install' first.");
        return 1;
    }

    return await HandleAddPathsAsync(credentials, config, paths);
}

static void EnsureBinOnPath(string binDir)
{
    var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
    if (pathEnv.Contains(binDir, StringComparison.OrdinalIgnoreCase))
    {
        return; // Already on PATH
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        try
        {
            var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            if (userPath.Contains(binDir, StringComparison.OrdinalIgnoreCase))
                return;

            var newPath = string.IsNullOrEmpty(userPath) ? binDir : $"{binDir};{userPath}";
            Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);
            Console.WriteLine($"Added {binDir} to user PATH.");
            Console.WriteLine("Open a new terminal for 'claudenest-agent' to be available.");
        }
        catch
        {
            Console.WriteLine($"Note: Add the following to your user PATH to use 'claudenest-agent' directly:");
            Console.WriteLine($"  {binDir}");
        }
    }
    else
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var shell = Environment.GetEnvironmentVariable("SHELL") ?? "";
        var profilePath = shell.EndsWith("zsh")
            ? Path.Combine(home, ".zshrc")
            : Path.Combine(home, ".bashrc");

        var exportLine = """export PATH="$HOME/.claudenest/bin:$PATH" """.Trim();

        try
        {
            var contents = File.Exists(profilePath) ? File.ReadAllText(profilePath) : "";
            if (contents.Contains(".claudenest/bin"))
                return;

            File.AppendAllText(profilePath, $"\n# ClaudeNest Agent\n{exportLine}\n");
            Console.WriteLine($"Added ~/.claudenest/bin to PATH in {Path.GetFileName(profilePath)}");
            Console.WriteLine($"Run 'source {profilePath}' or open a new terminal for 'claudenest-agent' to be available.");
        }
        catch
        {
            Console.WriteLine($"Note: Add ~/.claudenest/bin to your PATH to use 'claudenest-agent' directly:");
            Console.WriteLine($"  echo '{exportLine}' >> {profilePath}");
        }
    }
}

static async Task<int> HandleUninstallAsync()
{
    Console.WriteLine("Uninstalling ClaudeNest Agent...");

    using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
    var serviceLogger = loggerFactory.CreateLogger("ServiceInstaller");

    try
    {
        var installer = ServiceInstallerFactory.Create(serviceLogger);
        if (installer.IsInstalled())
        {
            Console.WriteLine("Stopping and removing background service...");
            await installer.UninstallAsync();
            Console.WriteLine("Service removed.");
        }
        else
        {
            Console.WriteLine("No background service found.");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Warning: Could not remove service: {ex.Message}");
    }

    // Remove installed binary
    var claudeNestDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claudenest");
    var binDir = Path.Combine(claudeNestDir, "bin");
    if (Directory.Exists(binDir))
    {
        try
        {
            Directory.Delete(binDir, true);
            Console.WriteLine("Removed installed binary.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Could not remove binary directory: {ex.Message}");
        }
    }

    // Clean up updates directory
    var updatesDir = Path.Combine(claudeNestDir, "updates");
    if (Directory.Exists(updatesDir))
    {
        try
        {
            Directory.Delete(updatesDir, true);
        }
        catch { /* best effort */ }
    }

    // Remove update marker
    var markerPath = Path.Combine(claudeNestDir, "update-pending");
    if (File.Exists(markerPath))
    {
        try { File.Delete(markerPath); } catch { /* best effort */ }
    }

    Console.WriteLine();
    Console.WriteLine("Agent uninstalled successfully.");
    Console.WriteLine("Note: Configuration and credentials are preserved in ~/.claudenest/");
    Console.WriteLine("To remove all data, delete the ~/.claudenest/ directory.");
    return 0;
}

static Task<bool> IsWorkspaceUntrustedAsync(string directoryPath)
{
    // Read trust status directly from ~/.claude.json instead of spawning processes.
    // Claude Code stores trust in: projects[normalizedPath].hasTrustDialogAccepted
    // It also walks up parent directories, so if a parent is trusted, children are too.
    try
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude.json");

        if (!File.Exists(configPath))
            return Task.FromResult(true); // No config = nothing trusted

        var json = File.ReadAllText(configPath);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("projects", out var projects))
            return Task.FromResult(true); // No projects = nothing trusted

        // Normalize the path (forward slashes, like Claude does)
        var normalized = Path.GetFullPath(directoryPath).Replace('\\', '/');

        // Check the exact path and walk up parent directories (same logic as Claude Code)
        var checkPath = normalized;
        while (true)
        {
            if (projects.TryGetProperty(checkPath, out var project) &&
                project.TryGetProperty("hasTrustDialogAccepted", out var accepted) &&
                accepted.GetBoolean())
            {
                return Task.FromResult(false); // Trusted
            }

            var parent = Path.GetDirectoryName(checkPath)?.Replace('\\', '/');
            if (parent is null || parent == checkPath)
                break;
            checkPath = parent;
        }

        return Task.FromResult(true); // Not trusted
    }
    catch
    {
        // If we can't read the config, don't block the install
        return Task.FromResult(false);
    }
}
