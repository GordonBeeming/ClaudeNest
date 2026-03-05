using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using ClaudeNest.Agent;
using ClaudeNest.Agent.Auth;
using ClaudeNest.Agent.Config;
using ClaudeNest.Agent.Serialization;
using ClaudeNest.Agent.ServiceInstall;

// Handle subcommands — first arg is always the command
if (args.Length == 0 || args[0] == "help")
{
    PrintHelp();
    return 0;
}

if (args[0] == "version")
{
    var version = typeof(AgentWorker).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    Console.WriteLine(version);
    return 0;
}

if (args[0] == "add-path")
{
    return await HandleAddPathCommand(args);
}

if (args[0] == "remove-path")
{
    return await HandleRemovePathCommand(args);
}

if (args[0] == "list-paths")
{
    return HandleListPaths();
}

if (args[0] == "status")
{
    return await HandleStatusAsync();
}

if (args[0] == "diag")
{
    return await HandleDiagAsync();
}

if (args[0] == "restart")
{
    return await HandleRestartAsync();
}

if (args[0] == "update")
{
    return await HandleUpdateAsync();
}

if (args[0] == "uninstall")
{
    return await HandleUninstallAsync();
}

// Handle 'install' subcommand: pair, register service, and optionally run interactively
if (args[0] == "install")
{
    var exitCode = await HandleInstallAsync(args);
    if (exitCode >= 0)
        return exitCode; // 0 = service installed and running, >0 = error

    // exitCode == -1 means service registration failed, fall through to run in foreground
    Console.WriteLine("Starting agent in foreground...");
    Console.WriteLine("Press Ctrl+C to stop.");
    Console.WriteLine();
}

if (args[0] != "run" && args[0] != "install")
{
    Console.WriteLine($"Unknown command: {args[0]}");
    Console.WriteLine();
    PrintHelp();
    return 1;
}

// Run the agent (works for both post-install and standalone 'run')

var hostArgs = args[1..]; // strip subcommand so the host doesn't choke on it

var builder = Host.CreateApplicationBuilder(hostArgs);

builder.AddServiceDefaults();

// Register as a Windows Service when running on Windows
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    builder.Services.AddWindowsService(o => o.ServiceName = "ClaudeNestAgent");
}

builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();
host.Run();
return 0;

static async Task<bool> ValidateAgentCredentialsAsync(AgentCredentials credentials)
{
    try
    {
        var handler = new HmacAuthHandler(credentials.AgentId, credentials.Secret);
        using var httpClient = new HttpClient(handler);

        var negotiateUrl = $"{credentials.BackendUrl.TrimEnd('/')}/hubs/nest/negotiate?negotiateVersion=1";
        var response = await httpClient.PostAsync(negotiateUrl, null);
        return response.IsSuccessStatusCode;
    }
    catch
    {
        // Network error — assume credentials are still valid (don't wipe on connectivity issues)
        return true;
    }
}

static async Task<int> HandleStatusAsync()
{
    var credentials = ConfigLoader.LoadCredentials();
    if (credentials is null)
    {
        Console.WriteLine("Not installed. Run 'claudenest-agent install' to get started.");
        return 1;
    }

    var config = ConfigLoader.LoadConfig();
    var version = typeof(AgentWorker).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    Console.WriteLine($"Agent:   {credentials.AgentId}");
    Console.WriteLine($"Version: {version}");
    Console.WriteLine($"Backend: {credentials.BackendUrl}");
    Console.WriteLine($"Paths:   {(config.AllowedPaths.Count > 0 ? string.Join(", ", config.AllowedPaths) : "(none)")}");

    using var loggerFactory = LoggerFactory.Create(_ => { });
    var installer = ServiceInstallerFactory.Create(loggerFactory.CreateLogger("status"));
    var serviceInstalled = installer.IsInstalled();
    var serviceRunning = serviceInstalled && await installer.IsRunningAsync();

    Console.WriteLine($"Service: {(serviceRunning ? "running" : serviceInstalled ? "installed but not running" : "not installed")}");

    return 0;
}

static async Task<int> HandleDiagAsync()
{
    Console.WriteLine("ClaudeNest Agent Diagnostics");
    Console.WriteLine(new string('-', 40));

    // 1. Check credentials
    var credentials = ConfigLoader.LoadCredentials();
    if (credentials is null)
    {
        Console.WriteLine("[FAIL] No credentials found. Run 'claudenest-agent install' to get started.");
        return 1;
    }
    Console.WriteLine("[OK]   Credentials file found");
    Console.WriteLine($"       Agent ID: {credentials.AgentId}");
    Console.WriteLine($"       Backend:  {credentials.BackendUrl}");

    // 2. Check config
    var config = ConfigLoader.LoadConfig();
    if (config.AllowedPaths.Count == 0)
    {
        Console.WriteLine("[WARN] No allowed paths configured. Use 'claudenest-agent add-path' to add directories.");
    }
    else
    {
        Console.WriteLine($"[OK]   {config.AllowedPaths.Count} allowed path(s) configured");
        foreach (var path in config.AllowedPaths)
        {
            var exists = Directory.Exists(path);
            Console.WriteLine($"       {(exists ? "[OK]  " : "[WARN]")} {path}{(exists ? "" : " (directory not found)")}");
        }
    }

    // 3. Check service
    using var loggerFactory = LoggerFactory.Create(_ => { });
    var installer = ServiceInstallerFactory.Create(loggerFactory.CreateLogger("diag"));
    var serviceInstalled = installer.IsInstalled();
    var serviceRunning = serviceInstalled && await installer.IsRunningAsync();

    if (serviceRunning)
        Console.WriteLine("[OK]   Service is running");
    else if (serviceInstalled)
        Console.WriteLine("[FAIL] Service is installed but not running. Try 'claudenest-agent uninstall' then 'claudenest-agent install'.");
    else
        Console.WriteLine("[FAIL] Service is not installed");

    // 4. Check backend connectivity
    Console.Write("       Checking backend connectivity... ");
    try
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var response = await httpClient.GetAsync(credentials.BackendUrl.TrimEnd('/'));
        Console.WriteLine("OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAILED");
        Console.WriteLine($"[FAIL] Cannot reach backend: {ex.Message}");
        return 1;
    }

    // 5. Check agent credentials against backend
    Console.Write("       Validating agent credentials... ");
    var isValid = await ValidateAgentCredentialsAsync(credentials);
    if (isValid)
    {
        Console.WriteLine("OK");
        Console.WriteLine("[OK]   Agent credentials are valid");
    }
    else
    {
        Console.WriteLine("FAILED");
        Console.WriteLine("[FAIL] Agent credentials are invalid. The agent may have been deleted from the web.");
        Console.WriteLine("       Run 'claudenest-agent uninstall' then 'claudenest-agent install' to re-pair.");
    }

    // 6. Check logs
    var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claudenest", "logs");
    var errorLog = Path.Combine(logDir, "agent-error.log");
    if (File.Exists(errorLog))
    {
        var errorInfo = new FileInfo(errorLog);
        if (errorInfo.Length > 0)
        {
            Console.WriteLine($"[INFO] Error log exists ({errorInfo.Length} bytes): {errorLog}");
            // Show last few lines
            var lines = await File.ReadAllLinesAsync(errorLog);
            var lastLines = lines.TakeLast(5).ToArray();
            if (lastLines.Length > 0)
            {
                Console.WriteLine("       Last error lines:");
                foreach (var line in lastLines)
                    Console.WriteLine($"         {line}");
            }
        }
    }

    return 0;
}

static void PrintHelp()
{
    Console.WriteLine("ClaudeNest Agent");
    Console.WriteLine();
    Console.WriteLine("Usage: claudenest-agent <command> [args]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  install       Pair with backend and register as a background service");
    Console.WriteLine("  restart       Restart the background service");
    Console.WriteLine("  update        Update the installed binary and restart the service");
    Console.WriteLine("  uninstall     Stop and remove the background service");
    Console.WriteLine("  add-path      Add directories to the agent");
    Console.WriteLine("  remove-path   Remove directories from the agent");
    Console.WriteLine("  list-paths    Show configured directories");
    Console.WriteLine("  status        Show agent status");
    Console.WriteLine("  diag          Run diagnostics and check connectivity");
    Console.WriteLine("  run           Run the agent in the foreground");
    Console.WriteLine("  version       Show the agent version");
    Console.WriteLine("  help          Show this help message");
}

static async Task<int> HandleInstallAsync(string[] args)
{
    string? token = null;
    string? backendUrl = null;
    string? agentName = null;
    string? servicePassword = null;
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
            case "--service-password" when i + 1 < args.Length:
                servicePassword = args[++i];
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
        // Verify the agent still exists on the backend
        var agentStillValid = await ValidateAgentCredentialsAsync(existingCredentials!);

        if (agentStillValid)
        {
            // Check if the service is actually running — if not, re-register it
            using var checkLoggerFactory = LoggerFactory.Create(_ => { });
            var checkInstaller = ServiceInstallerFactory.Create(checkLoggerFactory.CreateLogger("check"));
            if (!checkInstaller.IsInstalled())
            {
                Console.WriteLine("Agent is paired but the background service is not installed. Re-registering service...");
                return await InstallBinaryAndService(existingCredentials!, existingConfig.Name ?? Environment.MachineName, existingConfig.AllowedPaths, servicePassword);
            }

            Console.WriteLine("An agent is already installed on this machine.");
            Console.WriteLine();
            Console.WriteLine("To add directories to the existing agent:");
            Console.WriteLine("  claudenest-agent add-path /path/to/directory");
            Console.WriteLine();
            Console.WriteLine("To remove the existing agent and start fresh:");
            Console.WriteLine("  claudenest-agent uninstall");
            return 1;
        }

        // Agent was deleted from the backend — clean up local state
        Console.WriteLine("Previous agent was removed from the server. Cleaning up local state...");
        ConfigLoader.DeleteCredentials();
        ConfigLoader.DeleteConfig();
    }

    // New install — require token and backend
    if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(backendUrl))
    {
        Console.Error.WriteLine("Usage: claudenest-agent install --token <TOKEN> --backend <URL> [--name <NAME>] [--path <PATH> ...] [--service-password <PASSWORD>]");
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

    return await InstallBinaryAndService(credentials, agentName, paths, servicePassword);
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
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var currentBinaryPath = Environment.ProcessPath;
            var binaryPath = existingConfig.InstalledBinaryPath;

            // If running from a different (newer) binary, install it with versioned naming
            if (currentBinaryPath is not null && binaryPath is not null && currentBinaryPath != binaryPath)
            {
                var version = typeof(AgentWorker).Assembly.GetName().Version?.ToString() ?? "0.0.0";
                var ext = isWindows ? ".exe" : "";
                var binDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claudenest", "bin");
                var newVersionedPath = Path.Combine(binDir, $"claudenest-agent-{version}{ext}");

                Console.WriteLine($"Updating agent binary to {newVersionedPath}...");
                File.Copy(currentBinaryPath, newVersionedPath, overwrite: true);
                if (!isWindows)
                {
                    File.SetUnixFileMode(newVersionedPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }

                existingConfig.InstalledBinaryPath = newVersionedPath;
                ConfigLoader.SaveConfig(existingConfig);
                await installer.UpdateBinPathAsync(newVersionedPath);

                // Update symlink/copy
                var conveniencePath = Path.Combine(binDir, $"claudenest-agent{ext}");
                if (isWindows)
                {
                    File.Copy(newVersionedPath, conveniencePath, overwrite: true);
                }
                else
                {
                    if (File.Exists(conveniencePath) || new FileInfo(conveniencePath).LinkTarget is not null)
                        File.Delete(conveniencePath);
                    File.CreateSymbolicLink(conveniencePath, $"claudenest-agent-{version}{ext}");
                }
            }

            await installer.RestartAsync();
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

static async Task<int> InstallBinaryAndService(AgentCredentials credentials, string agentName, List<string> paths, string? servicePassword = null)
{
    // Copy binary to ~/.claudenest/bin/ with versioned naming
    var claudeNestDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claudenest");
    var binDir = Path.Combine(claudeNestDir, "bin");
    Directory.CreateDirectory(binDir);

    var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    var version = typeof(AgentWorker).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    var ext = isWindows ? ".exe" : "";
    var versionedBinaryName = $"claudenest-agent-{version}{ext}";
    var convenienceName = $"claudenest-agent{ext}";
    var versionedBinaryPath = Path.Combine(binDir, versionedBinaryName);
    var conveniencePath = Path.Combine(binDir, convenienceName);
    var currentBinaryPath = Environment.ProcessPath;

    if (currentBinaryPath is not null && currentBinaryPath != versionedBinaryPath)
    {
        Console.WriteLine($"Copying agent binary to {versionedBinaryPath}...");
        File.Copy(currentBinaryPath, versionedBinaryPath, overwrite: true);

        // Make executable on Unix
        if (!isWindows)
        {
            File.SetUnixFileMode(versionedBinaryPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    // Create symlink (Unix) or copy (Windows) for CLI convenience
    if (isWindows)
    {
        File.Copy(versionedBinaryPath, conveniencePath, overwrite: true);
    }
    else
    {
        if (File.Exists(conveniencePath) || new FileInfo(conveniencePath).LinkTarget is not null)
        {
            File.Delete(conveniencePath);
        }
        File.CreateSymbolicLink(conveniencePath, versionedBinaryName);
    }

    // Add ~/.claudenest/bin to PATH
    EnsureBinOnPath(binDir);

    // Determine service type
    var serviceType = isWindows ? "windows-service" :
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos-launchagent" :
        "linux-systemd";

    // Save config with name, allowed paths, and service info
    var nestConfig = new NestConfig
    {
        Name = agentName,
        AllowedPaths = paths,
        InstalledBinaryPath = versionedBinaryPath,
        ServiceType = serviceType
    };
    ConfigLoader.SaveConfig(nestConfig);

    // Register and start as a service
    Console.WriteLine("Registering as a background service...");
    using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
    var serviceLogger = loggerFactory.CreateLogger("ServiceInstaller");

    var options = servicePassword is not null
        ? new ServiceInstallOptions { ServicePassword = servicePassword }
        : null;

    try
    {
        var installer = ServiceInstallerFactory.Create(serviceLogger);
        var installResult = await installer.InstallAsync(versionedBinaryPath, options);
        if (installResult)
        {
            Console.WriteLine();
            Console.WriteLine($"Agent paired and installed successfully!");
            Console.WriteLine($"  Agent ID:    {credentials.AgentId}");
            Console.WriteLine($"  Name:        {agentName}");
            Console.WriteLine($"  Binary:      {versionedBinaryPath}");
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
            if (isWindows)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Service registration failed. On Windows, install requires an elevated (Administrator) terminal.");
                Console.Error.WriteLine("The agent has been paired — re-run install from an Administrator terminal to register the service.");
                Console.Error.WriteLine("Or run 'claudenest-agent run' to start the agent in the foreground.");
                return 1;
            }
            else
            {
                Console.WriteLine("Warning: Service registration failed. The agent will run in the foreground.");
            }
        }
    }
    catch (Exception ex)
    {
        if (isWindows)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Service registration failed: {ex.Message}");
            Console.Error.WriteLine("On Windows, install requires an elevated (Administrator) terminal.");
            Console.Error.WriteLine("The agent has been paired — re-run install from an Administrator terminal to register the service.");
            return 1;
        }
        else
        {
            Console.WriteLine($"Warning: Could not register as a service ({ex.Message}). The agent will run in the foreground.");
        }
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
    for (var i = 1; i < args.Length; i++)
    {
        paths.Add(Path.GetFullPath(args[i]));
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

static async Task<int> HandleRemovePathCommand(string[] args)
{
    List<string> paths = [];
    for (var i = 1; i < args.Length; i++)
    {
        paths.Add(Path.GetFullPath(args[i]));
    }

    var credentials = ConfigLoader.LoadCredentials();
    var config = ConfigLoader.LoadConfig();

    if (credentials is null || config.AllowedPaths.Count == 0)
    {
        Console.Error.WriteLine("No agent installed. Run 'claudenest-agent install' first.");
        return 1;
    }

    if (paths.Count == 0)
    {
        Console.Error.WriteLine("Usage: claudenest-agent remove-path /path/to/directory");
        return 1;
    }

    var removed = new List<string>();
    foreach (var p in paths)
    {
        var match = config.AllowedPaths.FirstOrDefault(existing =>
            string.Equals(Path.GetFullPath(existing), p, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            config.AllowedPaths.Remove(match);
            removed.Add(match);
        }
        else
        {
            Console.WriteLine($"Path not found in config: {p}");
        }
    }

    if (removed.Count == 0)
    {
        Console.WriteLine("No paths were removed.");
        return 0;
    }

    if (config.AllowedPaths.Count == 0)
    {
        Console.Error.WriteLine("Cannot remove all paths — at least one allowed path is required.");
        Console.Error.WriteLine("Use 'claudenest-agent uninstall' to fully remove the agent.");
        // Restore removed paths
        config.AllowedPaths.AddRange(removed);
        return 1;
    }

    ConfigLoader.SaveConfig(config);

    Console.WriteLine($"Removed: {string.Join(", ", removed)}");
    Console.WriteLine($"Remaining paths: {string.Join(", ", config.AllowedPaths)}");

    // Restart service to pick up changes
    using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
    var serviceLogger = loggerFactory.CreateLogger("ServiceInstaller");
    try
    {
        var installer = ServiceInstallerFactory.Create(serviceLogger);
        if (installer.IsInstalled())
        {
            await installer.RestartAsync();
            Console.WriteLine("Agent restarted.");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Warning: Could not restart service ({ex.Message}). Restart manually.");
    }

    return 0;
}

static int HandleListPaths()
{
    var config = ConfigLoader.LoadConfig();
    var credentials = ConfigLoader.LoadCredentials();

    if (credentials is null)
    {
        Console.Error.WriteLine("No agent installed. Run 'claudenest-agent install' first.");
        return 1;
    }

    Console.WriteLine($"Agent: {config.Name ?? "unnamed"} (ID: {credentials.AgentId})");
    Console.WriteLine();

    if (config.AllowedPaths.Count == 0)
    {
        Console.WriteLine("No allowed paths configured.");
    }
    else
    {
        Console.WriteLine("Allowed paths:");
        foreach (var p in config.AllowedPaths)
            Console.WriteLine($"  {p}");
    }

    if (config.DeniedPaths.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Denied paths:");
        foreach (var p in config.DeniedPaths)
            Console.WriteLine($"  {p}");
    }

    return 0;
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

static async Task<int> HandleRestartAsync()
{
    var credentials = ConfigLoader.LoadCredentials();
    if (credentials is null)
    {
        Console.Error.WriteLine("No agent installed. Run 'claudenest-agent install' first.");
        return 1;
    }

    using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
    var serviceLogger = loggerFactory.CreateLogger("ServiceInstaller");
    var installer = ServiceInstallerFactory.Create(serviceLogger);

    if (!installer.IsInstalled())
    {
        Console.Error.WriteLine("No background service found. Run 'claudenest-agent install' to set up the service.");
        return 1;
    }

    Console.WriteLine("Restarting ClaudeNest Agent service...");
    var result = await installer.RestartAsync();
    if (result)
    {
        Console.WriteLine("Agent restarted successfully.");
        return 0;
    }

    Console.Error.WriteLine("Failed to restart the agent service.");
    return 1;
}

static async Task<int> HandleUpdateAsync()
{
    var credentials = ConfigLoader.LoadCredentials();
    var config = ConfigLoader.LoadConfig();

    if (credentials is null || config.AllowedPaths.Count == 0)
    {
        Console.Error.WriteLine("No agent installed. Run 'claudenest-agent install' first.");
        return 1;
    }

    var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    var currentBinaryPath = Environment.ProcessPath;
    if (currentBinaryPath is null)
    {
        Console.Error.WriteLine("Cannot determine current binary path.");
        return 1;
    }

    // Determine new versioned path
    var version = typeof(AgentWorker).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    var ext = isWindows ? ".exe" : "";
    var claudeNestDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claudenest");
    var binDir = Path.Combine(claudeNestDir, "bin");
    Directory.CreateDirectory(binDir);

    var versionedBinaryName = $"claudenest-agent-{version}{ext}";
    var versionedBinaryPath = Path.Combine(binDir, versionedBinaryName);
    var convenienceName = $"claudenest-agent{ext}";
    var conveniencePath = Path.Combine(binDir, convenienceName);

    // Copy current binary to new versioned path (no need to stop service — writing to a new file)
    if (currentBinaryPath != versionedBinaryPath)
    {
        Console.WriteLine($"Installing agent binary to {versionedBinaryPath}...");
        File.Copy(currentBinaryPath, versionedBinaryPath, overwrite: true);
        if (!isWindows)
        {
            File.SetUnixFileMode(versionedBinaryPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        Console.WriteLine("Binary installed.");
    }
    else
    {
        Console.WriteLine("Already running from the versioned location. No binary copy needed.");
    }

    // Update config
    var oldBinaryPath = config.InstalledBinaryPath;
    config.InstalledBinaryPath = versionedBinaryPath;
    ConfigLoader.SaveConfig(config);

    // Update service registration to point to new binary
    using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
    var serviceLogger = loggerFactory.CreateLogger("ServiceInstaller");
    var installer = ServiceInstallerFactory.Create(serviceLogger);

    if (installer.IsInstalled())
    {
        Console.WriteLine("Updating service binary path...");
        await installer.UpdateBinPathAsync(versionedBinaryPath);

        Console.WriteLine("Restarting agent service...");
        await installer.RestartAsync();
        Console.WriteLine("Agent updated and restarted successfully.");
    }
    else
    {
        Console.WriteLine("No background service found. Binary updated but service not restarted.");
        Console.WriteLine("Run 'claudenest-agent install' to set up the service.");
    }

    // Update symlink/copy for CLI convenience
    if (isWindows)
    {
        File.Copy(versionedBinaryPath, conveniencePath, overwrite: true);
    }
    else
    {
        if (File.Exists(conveniencePath) || new FileInfo(conveniencePath).LinkTarget is not null)
        {
            File.Delete(conveniencePath);
        }
        File.CreateSymbolicLink(conveniencePath, versionedBinaryName);
    }

    // Clean up old versioned binary if different
    if (oldBinaryPath is not null && oldBinaryPath != versionedBinaryPath && File.Exists(oldBinaryPath))
    {
        try
        {
            File.Delete(oldBinaryPath);
            Console.WriteLine($"Cleaned up old binary: {Path.GetFileName(oldBinaryPath)}");
        }
        catch { /* best effort — may be in use on Windows */ }
    }

    return 0;
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

    // Kill any lingering agent processes (on Windows, schtasks /end may not kill immediately)
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        try
        {
            var currentPid = Environment.ProcessId;
            foreach (var proc in Process.GetProcessesByName("claudenest-agent"))
            {
                if (proc.Id != currentPid)
                {
                    Console.WriteLine($"Stopping agent process (PID {proc.Id})...");
                    proc.Kill(entireProcessTree: true);
                    proc.WaitForExit(5000);
                }
                proc.Dispose();
            }
        }
        catch { /* best effort */ }

        // Brief delay to let OS release file handles
        await Task.Delay(1000);
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
        catch
        {
            // On Windows, if we ARE the installed binary, we can't delete ourselves.
            // Spawn a background cmd process that waits for us to exit, then cleans up.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var pid = Environment.ProcessId;
                    var script = $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{binDir}\"";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = script,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    Console.WriteLine("Binary cleanup scheduled (will complete after this process exits).");
                }
                catch
                {
                    Console.Error.WriteLine($"Warning: Could not remove binary directory. You can manually delete it:");
                    Console.Error.WriteLine($"  rmdir /s /q \"{binDir}\"");
                }
            }
            else
            {
                Console.Error.WriteLine($"Warning: Could not remove binary directory: {binDir}");
            }
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
