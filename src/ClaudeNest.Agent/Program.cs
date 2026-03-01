using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using ClaudeNest.Agent;
using ClaudeNest.Agent.Config;
using ClaudeNest.Agent.Serialization;

// Handle 'install' subcommand: pair then run interactively
if (args.Length > 0 && args[0] == "install")
{
    var exitCode = await HandleInstallAsync(args);
    if (exitCode != 0)
        return exitCode;

    Console.WriteLine("Starting agent...");
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

    // Save config with name and allowed paths
    var nestConfig = new NestConfig
    {
        Name = agentName,
        AllowedPaths = paths
    };
    ConfigLoader.SaveConfig(nestConfig);

    Console.WriteLine($"Agent paired successfully! Agent ID: {credentials.AgentId}");
    Console.WriteLine($"Name: {agentName}");
    Console.WriteLine($"Allowed paths: {string.Join(", ", paths)}");
    Console.WriteLine("Configuration saved to ~/.claudenest/");
    return 0;
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
