using System.Text.Json;
using ClaudeNest.Backend.Data;
using ClaudeNest.Shared.Messages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Hubs;

public class NestHub(NestDbContext db, TimeProvider timeProvider) : Hub
{
    // --- Agent → Server ---

    public async Task<object> RegisterAgent(AgentInfo agentInfo)
    {
        var connectionId = Context.ConnectionId;
        AgentConnectionMap.AddOrUpdate(agentInfo.AgentId, connectionId);

        await Groups.AddToGroupAsync(connectionId, $"agent:{agentInfo.AgentId}");

        // Update agent status in DB
        var agent = await db.Agents
            .Include(a => a.Account)
            .ThenInclude(a => a!.Plan)
            .FirstOrDefaultAsync(a => a.Id == agentInfo.AgentId);
        if (agent is not null)
        {
            agent.IsOnline = true;
            agent.LastSeenAt = timeProvider.GetUtcNow();
            if (agentInfo.Name is not null)
                agent.Name = agentInfo.Name;
            agent.Hostname = agentInfo.Hostname;
            agent.OS = agentInfo.OS;
            agent.AllowedPathsJson = agentInfo.AllowedPaths.Count > 0
                ? JsonSerializer.Serialize(agentInfo.AllowedPaths)
                : null;
            await db.SaveChangesAsync();
        }

        // Notify web clients that this agent is online
        await Clients.Group($"user:{agentInfo.AgentId}")
            .SendAsync("AgentStatusChanged", agentInfo.AgentId, true);

        return new { EffectiveMaxSessions = agent?.Account?.Plan?.MaxSessions ?? 3 };
    }

    public async Task SessionStatusChanged(SessionStatusUpdate update)
    {
        // Persist session state to DB
        var session = await db.Sessions.FindAsync(update.SessionId);
        if (session is not null)
        {
            session.State = update.State.ToString();
            session.Pid = update.Pid;
            session.EndedAt = update.EndedAt;
            session.ExitCode = update.ExitCode;
            await db.SaveChangesAsync();
        }
        else
        {
            db.Sessions.Add(new Data.Entities.Session
            {
                Id = update.SessionId,
                AgentId = update.AgentId,
                Path = update.Path,
                State = update.State.ToString(),
                Pid = update.Pid,
                StartedAt = update.StartedAt,
                EndedAt = update.EndedAt,
                ExitCode = update.ExitCode
            });
            await db.SaveChangesAsync();
        }

        // Relay session status to web clients watching this agent
        await Clients.Group($"user:{update.AgentId}")
            .SendAsync("SessionStatusChanged", update);
    }

    public async Task DirectoryListing(DirectoryListingResponse response)
    {
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

    public async Task Heartbeat()
    {
        // Update agent last seen time
        var agentId = AgentConnectionMap.GetAgentByConnection(Context.ConnectionId);
        if (agentId.HasValue)
        {
            var agent = await db.Agents.FindAsync(agentId.Value);
            if (agent is not null)
            {
                agent.LastSeenAt = timeProvider.GetUtcNow();
                await db.SaveChangesAsync();
            }
        }
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
        // Enforce account-wide session limit
        var agent = await db.Agents
            .Include(a => a.Account)
            .ThenInclude(a => a!.Plan)
            .FirstOrDefaultAsync(a => a.Id == agentId);

        if (agent?.Account is not null)
        {
            var now = timeProvider.GetUtcNow();

            // Check subscription status
            var status = agent.Account.SubscriptionStatus;
            if (status != Shared.Enums.SubscriptionStatus.Active && status != Shared.Enums.SubscriptionStatus.Trialing)
            {
                await Clients.Group($"user:{agentId}")
                    .SendAsync("SessionStatusChanged", new SessionStatusUpdate
                    {
                        SessionId = sessionId,
                        AgentId = agentId,
                        Path = path,
                        State = Shared.Enums.SessionState.Crashed,
                        StartedAt = now.UtcDateTime,
                        EndedAt = now.UtcDateTime
                    });
                return;
            }

            // Check trial expiry via coupon redemptions
            if (status == Shared.Enums.SubscriptionStatus.Trialing)
            {
                var hasActiveTrial = await db.CouponRedemptions
                    .AnyAsync(cr => cr.AccountId == agent.AccountId && cr.FreeUntil > now);
                if (!hasActiveTrial)
                {
                    await Clients.Group($"user:{agentId}")
                        .SendAsync("SessionStatusChanged", new SessionStatusUpdate
                        {
                            SessionId = sessionId,
                            AgentId = agentId,
                            Path = path,
                            State = Shared.Enums.SessionState.Crashed,
                            StartedAt = now.UtcDateTime,
                            EndedAt = now.UtcDateTime
                        });
                    return;
                }
            }

            // Check account-wide session count
            var maxSessions = agent.Account.Plan?.MaxSessions ?? 0;
            var activeSessionCount = await db.Sessions.CountAsync(s =>
                s.Agent.AccountId == agent.AccountId &&
                (s.State == "Running" || s.State == "Starting" || s.State == "Requested"));

            if (activeSessionCount >= maxSessions)
            {
                await Clients.Group($"user:{agentId}")
                    .SendAsync("SessionStatusChanged", new SessionStatusUpdate
                    {
                        SessionId = sessionId,
                        AgentId = agentId,
                        Path = path,
                        State = Shared.Enums.SessionState.Crashed,
                        StartedAt = now.UtcDateTime,
                        EndedAt = now.UtcDateTime
                    });
                return;
            }
        }

        var permissionMode = agent?.Account?.PermissionMode ?? "default";

        if (AgentConnectionMap.TryGetConnectionId(agentId, out var agentConnectionId))
        {
            await Clients.Client(agentConnectionId)
                .SendAsync("StartSession", sessionId, path, permissionMode);
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
        var agentId = AgentConnectionMap.GetAgentByConnection(Context.ConnectionId);
        AgentConnectionMap.RemoveByConnection(Context.ConnectionId);

        if (agentId.HasValue)
        {
            // Mark agent offline in DB
            var agent = await db.Agents.FindAsync(agentId.Value);
            if (agent is not null)
            {
                agent.IsOnline = false;
                agent.LastSeenAt = timeProvider.GetUtcNow();
                await db.SaveChangesAsync();
            }

            // Notify web clients
            await Clients.Group($"user:{agentId.Value}")
                .SendAsync("AgentStatusChanged", agentId.Value, false);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // In-memory mappings (will move to a proper service later for multi-instance support)
    private static readonly AgentConnectionMap AgentConnectionMap = new();
    private static readonly PendingRequestMap PendingRequests = new();

    /// <summary>
    /// Allows controllers to check if an agent is connected and get its connection ID.
    /// </summary>
    public static bool TryGetAgentConnectionId(Guid agentId, out string connectionId)
        => AgentConnectionMap.TryGetConnectionId(agentId, out connectionId);
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

    public Guid? GetAgentByConnection(string connectionId)
    {
        lock (_lock)
        {
            return _connectionToAgent.TryGetValue(connectionId, out var agentId) ? agentId : null;
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
