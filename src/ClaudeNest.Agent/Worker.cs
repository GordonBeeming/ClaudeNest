using System.Runtime.InteropServices;
using ClaudeNest.Agent.Config;
using ClaudeNest.Agent.Services;
using ClaudeNest.Shared.Enums;
using ClaudeNest.Shared.Messages;

namespace ClaudeNest.Agent;

public class AgentWorker(
    ILogger<AgentWorker> logger,
    IConfiguration configuration,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    private SignalRConnectionManager? _connectionManager;
    private SessionManager? _sessionManager;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = ConfigLoader.LoadConfig();
        MergeConfigFromEnvironment(config, configuration);
        var credentials = LoadCredentials(configuration);

        if (credentials is null)
        {
            logger.LogError("No credentials found. Run 'claudenest install --token <TOKEN>' first.");
            lifetime.StopApplication();
            return;
        }

        var directoryBrowser = new DirectoryBrowser(config);

        var sessionManagerLogger = LoggerFactory.Create(b => b.AddConsole())
            .CreateLogger<SessionManager>();
        _sessionManager = new SessionManager(config, directoryBrowser, credentials.AgentId, sessionManagerLogger);

        _connectionManager = new SignalRConnectionManager(credentials, LoggerFactory.Create(b => b.AddConsole())
            .CreateLogger<SignalRConnectionManager>());

        // Wire up session status changes to SignalR
        _sessionManager.OnSessionStatusChanged += async update =>
        {
            try
            {
                await _connectionManager.SendSessionStatusAsync(update);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send session status update");
            }
        };

        // Wire up SignalR commands
        _connectionManager.OnListDirectories += async (requestId, path) =>
        {
            var directories = directoryBrowser.List(path);
            await _connectionManager.SendDirectoryListingAsync(new DirectoryListingResponse
            {
                RequestId = requestId,
                Path = path,
                Directories = directories
            });
        };

        _connectionManager.OnStartSession += async (sessionId, path, permissionMode) =>
        {
            if (!_sessionManager.TryStartSession(sessionId, path, permissionMode, out var error))
            {
                logger.LogWarning("Failed to start session {SessionId}: {Error}", sessionId, error);
                // Notify the frontend that the session failed to start
                await _connectionManager.SendSessionStatusAsync(new SessionStatusUpdate
                {
                    SessionId = sessionId,
                    AgentId = credentials.AgentId,
                    Path = path,
                    State = SessionState.Crashed,
                    StartedAt = DateTime.UtcNow,
                    EndedAt = DateTime.UtcNow
                });
            }
        };

        _connectionManager.OnStopSession += sessionId =>
        {
            _sessionManager.TryStopSession(sessionId);
            return Task.CompletedTask;
        };

        _connectionManager.OnGetSessions += async () =>
        {
            var sessions = _sessionManager.GetAllSessions();
            await _connectionManager.ReportAllSessionsAsync(credentials.AgentId, sessions);
        };

        _connectionManager.OnDeregister += () =>
        {
            logger.LogWarning("Received deregister command from backend. Stopping sessions and removing credentials.");
            _sessionManager.StopAllSessions();
            ConfigLoader.DeleteCredentials();
            lifetime.StopApplication();
            return Task.CompletedTask;
        };

        // Connect to the backend
        try
        {
            await _connectionManager.ConnectAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to backend at {Url}", credentials.BackendUrl);
            lifetime.StopApplication();
            return;
        }

        // Register with the hub
        await _connectionManager.RegisterAgentAsync(new AgentInfo
        {
            AgentId = credentials.AgentId,
            Name = config.Name,
            Hostname = Environment.MachineName,
            OS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
                : "linux",
            AllowedPaths = config.AllowedPaths
        });

        logger.LogInformation("Agent registered successfully");

        // Report current sessions on connect
        var currentSessions = _sessionManager.GetAllSessions();
        if (currentSessions.Count > 0)
        {
            await _connectionManager.ReportAllSessionsAsync(credentials.AgentId, currentSessions);
        }

        logger.LogInformation("Agent registered and connected. Waiting for commands...");

        // Main loop: heartbeat + health check
        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await heartbeatTimer.WaitForNextTickAsync(stoppingToken);
                _sessionManager.HealthCheck();
                await _connectionManager.SendHeartbeatAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error during heartbeat/health check");
            }
        }
    }

    private static void MergeConfigFromEnvironment(NestConfig config, IConfiguration configuration)
    {
        // Agent name from env
        var name = configuration["Agent:Name"];
        if (!string.IsNullOrEmpty(name))
            config.Name = name;

        // Merge allowed paths from env (e.g. Agent__AllowedPaths__0, Agent__AllowedPaths__1)
        var envPaths = configuration.GetSection("Agent:AllowedPaths").Get<string[]>();
        if (envPaths is { Length: > 0 })
        {
            foreach (var path in envPaths)
            {
                if (!string.IsNullOrWhiteSpace(path) && !config.AllowedPaths.Contains(path))
                    config.AllowedPaths.Add(path);
            }
        }
    }

    private static AgentCredentials? LoadCredentials(IConfiguration configuration)
    {
        // First try from IConfiguration (Aspire/env), then fall back to file
        var agentId = configuration["Agent:AgentId"];
        var secret = configuration["Agent:Secret"];
        var backendUrl = configuration["Agent:BackendUrl"];

        if (!string.IsNullOrEmpty(agentId) && !string.IsNullOrEmpty(secret) && !string.IsNullOrEmpty(backendUrl))
        {
            return new AgentCredentials
            {
                AgentId = Guid.Parse(agentId),
                Secret = secret,
                BackendUrl = backendUrl
            };
        }

        return ConfigLoader.LoadCredentials();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connectionManager is not null)
            await _connectionManager.DisposeAsync();

        await base.StopAsync(cancellationToken);
    }
}
