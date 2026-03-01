using System.Runtime.InteropServices;
using ClaudeNest.Agent.Config;
using ClaudeNest.Agent.Services;
using ClaudeNest.Shared.Messages;

namespace ClaudeNest.Agent;

public class AgentWorker(
    ILogger<AgentWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    private SignalRConnectionManager? _connectionManager;
    private SessionManager? _sessionManager;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = ConfigLoader.LoadConfig();
        var credentials = LoadCredentials(configuration);

        if (credentials is null)
        {
            logger.LogError("No credentials found. Run 'claudenest install --token <TOKEN>' first.");
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

        _connectionManager.OnStartSession += (sessionId, path) =>
        {
            if (!_sessionManager.TryStartSession(sessionId, path, out var error))
            {
                logger.LogWarning("Failed to start session {SessionId}: {Error}", sessionId, error);
            }
            return Task.CompletedTask;
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

        // Connect to the backend
        try
        {
            await _connectionManager.ConnectAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to backend at {Url}", credentials.BackendUrl);
            return;
        }

        // Register with the hub
        await _connectionManager.RegisterAgentAsync(new AgentInfo
        {
            AgentId = credentials.AgentId,
            Hostname = Environment.MachineName,
            OS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
                : "linux",
            MaxSessions = config.MaxSessions,
            AllowedPaths = config.AllowedPaths
        });

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
