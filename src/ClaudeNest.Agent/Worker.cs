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
    private AgentUpdater? _updater;

    // Deferred update: binary is downloaded but waiting for sessions to end
    private string? _pendingUpdateBinaryPath;
    private UpdateAvailableNotification? _pendingUpdate;
    private Guid _pendingUpdateAgentId;

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

        using var logFactory = LoggerFactory.Create(b => b.AddConsole());
        var sessionManagerLogger = logFactory.CreateLogger<SessionManager>();
        _sessionManager = new SessionManager(config, directoryBrowser, credentials.AgentId, sessionManagerLogger);

        _updater = new AgentUpdater(logFactory.CreateLogger<AgentUpdater>(), lifetime);

        _connectionManager = new SignalRConnectionManager(credentials, logFactory
            .CreateLogger<SignalRConnectionManager>());

        // Re-register agent on reconnection
        _connectionManager.OnReconnected += async _ =>
        {
            try
            {
                var result = await _connectionManager.RegisterAgentAsync(new AgentInfo
                {
                    AgentId = credentials.AgentId,
                    Name = config.Name,
                    Hostname = Environment.MachineName,
                    OS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
                        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
                        : "linux",
                    Version = typeof(AgentWorker).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                    Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    AllowedPaths = config.AllowedPaths
                });
                logger.LogInformation("Agent re-registered after reconnection");

                // Report current sessions on reconnect
                if (_sessionManager is not null)
                {
                    var currentSessions = _sessionManager.GetAllSessions();
                    if (currentSessions.Count > 0)
                        await _connectionManager.ReportAllSessionsAsync(credentials.AgentId, currentSessions);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to re-register after reconnection");
            }
        };

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

        // Wire up auto-update handlers
        _connectionManager.OnUpdateAvailable += async notification =>
        {
            logger.LogInformation("Update available: v{Version}", notification.LatestVersion);
            await HandleUpdateAsync(notification, credentials.AgentId, stoppingToken);
        };

        _connectionManager.OnTriggerUpdate += async notification =>
        {
            logger.LogInformation("Update triggered from backend: v{Version}", notification.LatestVersion);
            await HandleUpdateAsync(notification, credentials.AgentId, stoppingToken);
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
        AgentRegistrationResult? registrationResult;
        try
        {
            registrationResult = await _connectionManager.RegisterAgentAsync(new AgentInfo
            {
                AgentId = credentials.AgentId,
                Name = config.Name,
                Hostname = Environment.MachineName,
                OS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
                    : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
                    : "linux",
                Version = typeof(AgentWorker).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                AllowedPaths = config.AllowedPaths
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register agent (connection may have been lost). " +
                "The reconnection handler will re-register when connection is restored.");
            // Don't crash — let the reconnection handler in SignalRConnectionManager re-register.
            // Fall through to the heartbeat loop which will keep the process alive.
            registrationResult = null;
        }

        if (registrationResult is not null)
            logger.LogInformation("Agent registered successfully");

        // Check if we just completed an update
        var updateCompleted = await _updater.CheckAndCompleteUpdateAsync();
        if (updateCompleted)
        {
            var currentVersion = typeof(AgentWorker).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            logger.LogInformation("Update to version {Version} completed successfully", currentVersion);
            try
            {
                await _connectionManager.SendUpdateStatusAsync(new UpdateStatusReport
                {
                    AgentId = credentials.AgentId,
                    Status = "completed",
                    NewVersion = currentVersion
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to report update completion status");
            }
        }

        // Check if an update is available based on registration result
        if (registrationResult?.LatestAgentVersion is not null && registrationResult.UpdateDownloadUrl is not null)
        {
            var currentVersion = typeof(AgentWorker).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            if (currentVersion != registrationResult.LatestAgentVersion &&
                !string.IsNullOrEmpty(registrationResult.UpdateDownloadUrl))
            {
                logger.LogInformation(
                    "Newer version available: {LatestVersion} (current: {CurrentVersion}). Auto-updating...",
                    registrationResult.LatestAgentVersion, currentVersion);

                _ = HandleUpdateAsync(new UpdateAvailableNotification
                {
                    LatestVersion = registrationResult.LatestAgentVersion,
                    DownloadUrl = registrationResult.UpdateDownloadUrl
                }, credentials.AgentId, stoppingToken);
            }
        }

        // Reconcile sessions the server thinks are active with local process state
        if (registrationResult?.ActiveSessions is { Count: > 0 })
        {
            logger.LogInformation("Reconciling {Count} active sessions from server", registrationResult.ActiveSessions.Count);
            var reconciled = _sessionManager.ReconcileSessions(registrationResult.ActiveSessions);
            foreach (var update in reconciled)
            {
                try
                {
                    await _connectionManager.SendSessionStatusAsync(update);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to send reconciled session status for {SessionId}", update.SessionId);
                }
            }
        }

        // Report current sessions on connect
        var currentSessions = _sessionManager.GetAllSessions();
        if (currentSessions.Count > 0)
        {
            await _connectionManager.ReportAllSessionsAsync(credentials.AgentId, currentSessions);
        }

        logger.LogInformation("Agent registered and connected. Waiting for commands...");

        // Main loop: heartbeat + health check + deferred update check
        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await heartbeatTimer.WaitForNextTickAsync(stoppingToken);
                _sessionManager.HealthCheck();
                await _connectionManager.SendHeartbeatAsync();

                // Check if a deferred update can now be applied
                await TryApplyPendingUpdateAsync(stoppingToken);
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

    private async Task HandleUpdateAsync(UpdateAvailableNotification notification, Guid agentId, CancellationToken ct)
    {
        if (_updater is null || _connectionManager is null || _sessionManager is null) return;

        try
        {
            await _connectionManager.SendUpdateStatusAsync(new UpdateStatusReport
            {
                AgentId = agentId,
                Status = "downloading",
                NewVersion = notification.LatestVersion
            });

            // Download the binary first
            var binaryPath = await _updater.DownloadAsync(
                notification.DownloadUrl,
                notification.LatestVersion,
                ct);

            // If sessions are active, defer the update until they finish
            if (_sessionManager.HasActiveSessions())
            {
                logger.LogInformation(
                    "Update v{Version} downloaded but sessions are active. Deferring apply until sessions end.",
                    notification.LatestVersion);

                _pendingUpdateBinaryPath = binaryPath;
                _pendingUpdate = notification;
                _pendingUpdateAgentId = agentId;

                await _connectionManager.SendUpdateStatusAsync(new UpdateStatusReport
                {
                    AgentId = agentId,
                    Status = "waiting_for_sessions",
                    NewVersion = notification.LatestVersion
                });
                return;
            }

            // No active sessions — apply immediately
            await _connectionManager.SendUpdateStatusAsync(new UpdateStatusReport
            {
                AgentId = agentId,
                Status = "restarting",
                NewVersion = notification.LatestVersion
            });

            await _updater.ApplyAsync(
                binaryPath,
                notification.LatestVersion,
                async () => _sessionManager.StopAllSessions(),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auto-update failed");
            try
            {
                await _connectionManager.SendUpdateStatusAsync(new UpdateStatusReport
                {
                    AgentId = agentId,
                    Status = "failed",
                    Error = ex.Message,
                    NewVersion = notification.LatestVersion
                });
            }
            catch
            {
                // Best effort
            }
        }
    }

    private async Task TryApplyPendingUpdateAsync(CancellationToken ct)
    {
        if (_pendingUpdate is null || _pendingUpdateBinaryPath is null ||
            _updater is null || _connectionManager is null || _sessionManager is null)
            return;

        if (_sessionManager.HasActiveSessions())
            return;

        logger.LogInformation("All sessions ended. Applying deferred update v{Version}",
            _pendingUpdate.LatestVersion);

        var notification = _pendingUpdate;
        var binaryPath = _pendingUpdateBinaryPath;
        var agentId = _pendingUpdateAgentId;

        // Clear pending state before applying (prevent re-entry)
        _pendingUpdate = null;
        _pendingUpdateBinaryPath = null;

        try
        {
            await _connectionManager.SendUpdateStatusAsync(new UpdateStatusReport
            {
                AgentId = agentId,
                Status = "restarting",
                NewVersion = notification.LatestVersion
            });

            await _updater.ApplyAsync(
                binaryPath,
                notification.LatestVersion,
                async () => _sessionManager.StopAllSessions(),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Deferred update apply failed");
            try
            {
                await _connectionManager.SendUpdateStatusAsync(new UpdateStatusReport
                {
                    AgentId = agentId,
                    Status = "failed",
                    Error = ex.Message,
                    NewVersion = notification.LatestVersion
                });
            }
            catch
            {
                // Best effort
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
