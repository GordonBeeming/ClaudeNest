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

    public event Func<string, string, Task>? OnListDirectories;
    public event Func<Guid, string, string, Task>? OnStartSession;
    public event Func<Guid, Task>? OnStopSession;
    public event Func<Task>? OnGetSessions;
    public event Func<Task>? OnDeregister;
    public event Func<UpdateAvailableNotification, Task>? OnUpdateAvailable;
    public event Func<UpdateAvailableNotification, Task>? OnTriggerUpdate;

    public SignalRConnectionManager(AgentCredentials credentials, ILogger<SignalRConnectionManager> logger)
    {
        _credentials = credentials;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
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

        _connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR reconnected with connection ID: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        _connection.Closed += error =>
        {
            _logger.LogWarning(error, "SignalR connection closed");
            return Task.CompletedTask;
        };

        await _connection.StartAsync(cancellationToken);
        _logger.LogInformation("Connected to SignalR hub at {Url}", hubUrl);
    }

    public async Task<AgentRegistrationResult?> RegisterAgentAsync(AgentInfo agentInfo)
    {
        if (_connection is not null)
            return await _connection.InvokeAsync<AgentRegistrationResult>("RegisterAgent", agentInfo);
        return null;
    }

    public async Task SendSessionStatusAsync(SessionStatusUpdate update)
    {
        if (_connection is not null)
            await _connection.InvokeAsync("SessionStatusChanged", update);
    }

    public async Task SendDirectoryListingAsync(DirectoryListingResponse response)
    {
        if (_connection is not null)
            await _connection.InvokeAsync("DirectoryListing", response);
    }

    public async Task ReportAllSessionsAsync(Guid agentId, List<SessionStatusUpdate> sessions)
    {
        if (_connection is not null)
            await _connection.InvokeAsync("ReportAllSessions", agentId, sessions);
    }

    public async Task SendHeartbeatAsync()
    {
        if (_connection is not null)
            await _connection.InvokeAsync("Heartbeat");
    }

    public async Task SendUpdateStatusAsync(UpdateStatusReport report)
    {
        if (_connection is not null)
            await _connection.InvokeAsync("UpdateStatus", report);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
