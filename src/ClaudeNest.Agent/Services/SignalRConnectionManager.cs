using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClaudeNest.Agent.Auth;
using ClaudeNest.Agent.Config;
using ClaudeNest.Agent.Serialization;
using ClaudeNest.Shared.Messages;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace ClaudeNest.Agent.Services;

public sealed class SignalRConnectionManager : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly AgentCredentials _credentials;
    private readonly ILogger<SignalRConnectionManager> _logger;
    private CancellationToken _stoppingToken;

    public event Func<string, string, Task>? OnListDirectories;
    public event Func<Guid, string, string, Task>? OnStartSession;
    public event Func<Guid, Task>? OnStopSession;
    public event Func<Task>? OnGetSessions;
    public event Func<Task>? OnDeregister;
    public event Func<UpdateAvailableNotification, Task>? OnUpdateAvailable;
    public event Func<UpdateAvailableNotification, Task>? OnTriggerUpdate;
    public event Func<string?, Task>? OnReconnected;

    public SignalRConnectionManager(AgentCredentials credentials, ILogger<SignalRConnectionManager> logger)
    {
        _credentials = credentials;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        var hubUrl = $"{_credentials.BackendUrl.TrimEnd('/')}/hubs/nest";

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            TypeInfoResolverChain = { AgentJsonContext.Default }
        };

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = innerHandler =>
                    new HmacAuthHandler(_credentials.AgentId, _credentials.Secret, innerHandler);
                // AccessTokenProvider is needed for WebSocket connections where
                // HttpMessageHandlerFactory doesn't apply (WebSocket uses ClientWebSocket directly)
                options.AccessTokenProvider = () =>
                {
                    var timestamp = DateTimeOffset.UtcNow.ToString("O");
                    var derivedKey = SHA256.HashData(Encoding.UTF8.GetBytes(_credentials.Secret));
                    var message = $"{timestamp}|{_credentials.AgentId}";
                    var signature = HMACSHA256.HashData(derivedKey, Encoding.UTF8.GetBytes(message));
                    return Task.FromResult<string?>(
                        $"agent-hmac|{_credentials.AgentId}|{timestamp}|{Convert.ToBase64String(signature)}");
                };
            })
            .WithAutomaticReconnect()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions = jsonOptions;
            })
            .Build();

        // Register server-to-agent method handlers
        _connection.On<string, string>("ListDirectories", (requestId, path) =>
            OnListDirectories?.Invoke(requestId, path) ?? Task.CompletedTask);

        _connection.On<Guid, string, string>("StartSession", (sessionId, path, permissionMode) =>
            OnStartSession?.Invoke(sessionId, path, permissionMode) ?? Task.CompletedTask);

        _connection.On<Guid>("StopSession", sessionId =>
            OnStopSession?.Invoke(sessionId) ?? Task.CompletedTask);

        _connection.On("GetSessions", () =>
            OnGetSessions?.Invoke() ?? Task.CompletedTask);

        _connection.On<DeregisterCommand>("Deregister", _ =>
            OnDeregister?.Invoke() ?? Task.CompletedTask);

        _connection.On<UpdateAvailableNotification>("UpdateAvailable", notification =>
            OnUpdateAvailable?.Invoke(notification) ?? Task.CompletedTask);

        _connection.On<UpdateAvailableNotification>("TriggerUpdate", notification =>
            OnTriggerUpdate?.Invoke(notification) ?? Task.CompletedTask);

        _connection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "SignalR connection lost, reconnecting...");
            return Task.CompletedTask;
        };

        _connection.Reconnected += async connectionId =>
        {
            _logger.LogInformation("SignalR reconnected with connection ID: {ConnectionId}", connectionId);
            if (OnReconnected is not null)
                await OnReconnected(connectionId);
        };

        _connection.Closed += async error =>
        {
            _logger.LogWarning(error, "SignalR connection closed. Starting manual reconnection...");
            var delay = TimeSpan.FromSeconds(5);
            var maxDelay = TimeSpan.FromMinutes(5);
            while (!_stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(delay, _stoppingToken);
                    await _connection.StartAsync(_stoppingToken);
                    _logger.LogInformation("Manual reconnection successful");
                    if (OnReconnected is not null)
                        await OnReconnected(_connection.ConnectionId);
                    break;
                }
                catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Reconnection cancelled — agent is shutting down");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Manual reconnection failed, retrying in {Delay}s...", delay.TotalSeconds);
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, maxDelay.TotalSeconds));
                }
            }
        };

        await _connection.StartAsync(stoppingToken);
        _logger.LogInformation("Connected to SignalR hub at {Url}", hubUrl);
    }

    public async Task<AgentRegistrationResult?> RegisterAgentAsync(AgentInfo agentInfo, CancellationToken ct = default)
    {
        if (_connection is not null)
            return await _connection.InvokeAsync<AgentRegistrationResult>("RegisterAgent", agentInfo, ct);
        return null;
    }

    public async Task SendSessionStatusAsync(SessionStatusUpdate update, CancellationToken ct = default)
    {
        if (_connection is not null)
            await _connection.SendAsync("SessionStatusChanged", update, ct);
    }

    public async Task SendDirectoryListingAsync(DirectoryListingResponse response, CancellationToken ct = default)
    {
        if (_connection is not null)
            await _connection.SendAsync("DirectoryListing", response, ct);
    }

    public async Task ReportAllSessionsAsync(Guid agentId, List<SessionStatusUpdate> sessions, CancellationToken ct = default)
    {
        if (_connection is not null)
            await _connection.SendAsync("ReportAllSessions", agentId, sessions, ct);
    }

    public async Task SendHeartbeatAsync(CancellationToken ct = default)
    {
        if (_connection is not null)
            await _connection.SendAsync("Heartbeat", ct);
    }

    public async Task SendUpdateStatusAsync(UpdateStatusReport report, CancellationToken ct = default)
    {
        if (_connection is not null)
            await _connection.SendAsync("UpdateStatus", report, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
