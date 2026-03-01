using ClaudeNest.Shared.Messages;
using Microsoft.AspNetCore.SignalR;

namespace ClaudeNest.Backend.Hubs;

public class NestHub : Hub
{
    // --- Agent → Server ---

    public async Task RegisterAgent(AgentInfo agentInfo)
    {
        // Store the agent's connection mapping
        var connectionId = Context.ConnectionId;
        AgentConnectionMap.AddOrUpdate(agentInfo.AgentId, connectionId);

        await Groups.AddToGroupAsync(connectionId, $"agent:{agentInfo.AgentId}");

        // Notify web clients that this agent is online
        await Clients.Group($"user:{agentInfo.AgentId}")
            .SendAsync("AgentStatusChanged", agentInfo.AgentId, true);
    }

    public async Task SessionStatusChanged(SessionStatusUpdate update)
    {
        // Relay session status to web clients watching this agent
        await Clients.Group($"user:{update.AgentId}")
            .SendAsync("SessionStatusChanged", update);
    }

    public async Task DirectoryListing(DirectoryListingResponse response)
    {
        // Relay directory listing back to the requesting web client
        // The requestId encodes the web client's connection ID
        if (PendingRequests.TryRemove(response.RequestId, out var webConnectionId))
        {
            await Clients.Client(webConnectionId)
                .SendAsync("DirectoryListingResult", response);
        }
    }

    public async Task ReportAllSessions(Guid agentId, List<SessionStatusUpdate> sessions)
    {
        await Clients.Group($"user:{agentId}")
            .SendAsync("AllSessionsUpdated", agentId, sessions);
    }

    public Task Heartbeat()
    {
        // Agent keepalive — update last seen
        return Task.CompletedTask;
    }

    // --- Web Client → Server → Agent ---

    public async Task SubscribeToAgent(Guid agentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{agentId}");
    }

    public async Task RequestDirectoryListing(Guid agentId, string path)
    {
        var requestId = Guid.NewGuid().ToString();
        PendingRequests.TryAdd(requestId, Context.ConnectionId);

        if (AgentConnectionMap.TryGetConnectionId(agentId, out var agentConnectionId))
        {
            await Clients.Client(agentConnectionId)
                .SendAsync("ListDirectories", requestId, path);
        }
        else
        {
            await Clients.Caller.SendAsync("DirectoryListingResult", new DirectoryListingResponse
            {
                RequestId = requestId,
                Path = path,
                Error = "Agent is offline"
            });
        }
    }

    public async Task RequestStartSession(Guid agentId, Guid sessionId, string path)
    {
        if (AgentConnectionMap.TryGetConnectionId(agentId, out var agentConnectionId))
        {
            await Clients.Client(agentConnectionId)
                .SendAsync("StartSession", sessionId, path);
        }
    }

    public async Task RequestStopSession(Guid agentId, Guid sessionId)
    {
        if (AgentConnectionMap.TryGetConnectionId(agentId, out var agentConnectionId))
        {
            await Clients.Client(agentConnectionId)
                .SendAsync("StopSession", sessionId);
        }
    }

    public async Task RequestGetSessions(Guid agentId)
    {
        if (AgentConnectionMap.TryGetConnectionId(agentId, out var agentConnectionId))
        {
            await Clients.Client(agentConnectionId)
                .SendAsync("GetSessions");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        AgentConnectionMap.RemoveByConnection(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // In-memory mappings (will move to a proper service later for multi-instance support)
    private static readonly AgentConnectionMap AgentConnectionMap = new();
    private static readonly PendingRequestMap PendingRequests = new();
}

internal sealed class AgentConnectionMap
{
    private readonly Dictionary<Guid, string> _agentToConnection = new();
    private readonly Dictionary<string, Guid> _connectionToAgent = new();
    private readonly Lock _lock = new();

    public void AddOrUpdate(Guid agentId, string connectionId)
    {
        lock (_lock)
        {
            // Remove old connection for this agent if exists
            if (_agentToConnection.TryGetValue(agentId, out var oldConn))
            {
                _connectionToAgent.Remove(oldConn);
            }

            _agentToConnection[agentId] = connectionId;
            _connectionToAgent[connectionId] = agentId;
        }
    }

    public bool TryGetConnectionId(Guid agentId, out string connectionId)
    {
        lock (_lock)
        {
            return _agentToConnection.TryGetValue(agentId, out connectionId!);
        }
    }

    public void RemoveByConnection(string connectionId)
    {
        lock (_lock)
        {
            if (_connectionToAgent.TryGetValue(connectionId, out var agentId))
            {
                _connectionToAgent.Remove(connectionId);
                _agentToConnection.Remove(agentId);
            }
        }
    }
}

internal sealed class PendingRequestMap
{
    private readonly Dictionary<string, string> _requests = new();
    private readonly Lock _lock = new();

    public void TryAdd(string requestId, string connectionId)
    {
        lock (_lock)
        {
            _requests[requestId] = connectionId;
        }
    }

    public bool TryRemove(string requestId, out string connectionId)
    {
        lock (_lock)
        {
            if (_requests.TryGetValue(requestId, out connectionId!))
            {
                _requests.Remove(requestId);
                return true;
            }
            return false;
        }
    }
}
