using System.Text.Json;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Services;
using ClaudeNest.Shared.Messages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ClaudeNest.Backend.Hubs;

public class NestHub(NestDbContext db, TimeProvider timeProvider, IConfiguration configuration, ILogger<NestHub> logger, AgentTracker agentTracker) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var isAgent = httpContext?.Items.ContainsKey("AgentId") == true
            || Context.User?.FindFirst("AgentId") is not null; // Azure SignalR carries claims via JWT
        var isUser = Context.User?.Identity?.IsAuthenticated == true
            && Context.User.FindFirst("AgentId") is null; // Exclude agent ClaimsPrincipal

        if (!isAgent && !isUser)
        {
            logger.LogWarning("Unauthenticated SignalR connection rejected: {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }

    // --- Agent → Server ---

    public async Task<AgentRegistrationResult> RegisterAgent(AgentInfo agentInfo)
    {
        var connectionId = Context.ConnectionId;

        await Groups.AddToGroupAsync(connectionId, $"agent:{agentInfo.AgentId}");

        // Update agent status in DB
        var agent = await db.Agents
            .AsTracking()
            .Include(a => a.Account)
            .ThenInclude(a => a!.Plan)
            .FirstOrDefaultAsync(a => a.Id == agentInfo.AgentId);
        if (agent is not null)
        {
            agent.IsOnline = true;
            agent.ConnectionId = connectionId;
            agent.LastSeenAt = timeProvider.GetUtcNow();
            if (agentInfo.Name is not null)
                agent.Name = agentInfo.Name;
            agent.Hostname = agentInfo.Hostname;
            agent.OS = agentInfo.OS;
            agent.Version = agentInfo.Version;
            agent.Architecture = agentInfo.Architecture;
            agent.AllowedPathsJson = agentInfo.AllowedPaths.Count > 0
                ? JsonSerializer.Serialize(agentInfo.AllowedPaths)
                : null;
            await db.SaveChangesAsync();
        }

        // Notify web clients that this agent is online
        await Clients.Group($"user:{agentInfo.AgentId}")
            .SendAsync("AgentStatusChanged", agentInfo.AgentId, true);

        // Track agent online for admin summary
        if (agent is not null)
        {
            agentTracker.TrackOnline(agentInfo.AgentId, agent.AccountId);
            await BroadcastAdminAgentSummaryAsync();
        }

        // Fetch active sessions the server thinks this agent has
        var activeSessions = await db.Sessions
            .Where(s => s.AgentId == agentInfo.AgentId &&
                (s.State == "Running" || s.State == "Starting" || s.State == "Requested"))
            .Select(s => new ActiveSessionInfo
            {
                SessionId = s.Id,
                Path = s.Path,
                Pid = s.Pid
            })
            .ToListAsync();

        var latestVersion = configuration["Agent:LatestVersion"];
        // Don't send update info if the version is unset or the default placeholder
        var hasRealVersion = !string.IsNullOrEmpty(latestVersion) && latestVersion != "1.0.0";
        return new AgentRegistrationResult
        {
            EffectiveMaxSessions = agent?.Account?.Plan?.MaxSessions ?? 3,
            LatestAgentVersion = hasRealVersion ? latestVersion : null,
            UpdateDownloadUrl = hasRealVersion
                ? $"https://github.com/gordonbeeming/ClaudeNest/releases/download/agent-v{latestVersion}/"
                : null,
            ActiveSessions = activeSessions
        };
    }

    public async Task SessionStatusChanged(SessionStatusUpdate update)
    {
        // Persist session state to DB
        var session = await db.Sessions.AsTracking().FirstOrDefaultAsync(s => s.Id == update.SessionId);
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
        await Clients.Group($"request:{response.RequestId}")
            .SendAsync("DirectoryListingResult", response);
        // Clean up — remove all connections from the request group
        // (The caller is the agent, not in the group, so this is a best-effort cleanup;
        //  the web client will also be removed when it disconnects)
    }

    public async Task ReportAllSessions(Guid agentId, List<SessionStatusUpdate> sessions)
    {
        await Clients.Group($"user:{agentId}")
            .SendAsync("AllSessionsUpdated", agentId, sessions);
    }

    public async Task UpdateStatus(UpdateStatusReport report)
    {
        await Clients.Group($"user:{report.AgentId}")
            .SendAsync("AgentUpdateStatus", report);
    }

    public async Task Heartbeat()
    {
        var connectionId = Context.ConnectionId;
        var agent = await db.Agents
            .AsTracking()
            .FirstOrDefaultAsync(a => a.ConnectionId == connectionId);
        if (agent is not null)
        {
            agent.LastSeenAt = timeProvider.GetUtcNow();
            await db.SaveChangesAsync();
        }
        else
        {
            logger.LogWarning("Heartbeat received from unknown connection {ConnectionId}", connectionId);
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

        // Add the calling web client to a group for this request
        await Groups.AddToGroupAsync(Context.ConnectionId, $"request:{requestId}");

        var agentConnectionId = await db.Agents
            .Where(a => a.Id == agentId && a.IsOnline)
            .Select(a => a.ConnectionId)
            .FirstOrDefaultAsync();

        if (agentConnectionId is not null)
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
                        EndedAt = now.UtcDateTime,
                        ErrorMessage = $"Subscription is not active (status: {status}). Please update your plan."
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
                            EndedAt = now.UtcDateTime,
                            ErrorMessage = "Your trial has expired. Please subscribe to a plan."
                        });
                    return;
                }
            }

            // Check account-wide session count
            var maxSessions = agent.Account.Plan?.MaxSessions ?? 0;
            var activeSessionCount = await db.Sessions.CountAsync(s =>
                s.Agent.AccountId == agent.AccountId && s.Agent.IsOnline &&
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
                        EndedAt = now.UtcDateTime,
                        ErrorMessage = $"Session limit reached ({activeSessionCount}/{maxSessions}). Stop an active session or upgrade your plan."
                    });
                return;
            }
        }

        var permissionMode = agent?.Account?.PermissionMode ?? "default";

        var agentConnectionId = await db.Agents
            .Where(a => a.Id == agentId && a.IsOnline)
            .Select(a => a.ConnectionId)
            .FirstOrDefaultAsync();

        if (agentConnectionId is not null)
        {
            await Clients.Client(agentConnectionId)
                .SendAsync("StartSession", sessionId, path, permissionMode);
        }
    }

    public async Task RequestStopSession(Guid agentId, Guid sessionId)
    {
        var agentConnectionId = await db.Agents
            .Where(a => a.Id == agentId && a.IsOnline)
            .Select(a => a.ConnectionId)
            .FirstOrDefaultAsync();

        if (agentConnectionId is not null)
        {
            await Clients.Client(agentConnectionId)
                .SendAsync("StopSession", sessionId);
        }
    }

    public async Task RequestGetSessions(Guid agentId)
    {
        var agentConnectionId = await db.Agents
            .Where(a => a.Id == agentId && a.IsOnline)
            .Select(a => a.ConnectionId)
            .FirstOrDefaultAsync();

        if (agentConnectionId is not null)
        {
            await Clients.Client(agentConnectionId)
                .SendAsync("GetSessions");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connId = Context.ConnectionId;
        var agent = await db.Agents
            .AsTracking()
            .FirstOrDefaultAsync(a => a.ConnectionId == connId);

        if (agent is not null)
        {
            agent.IsOnline = false;
            agent.ConnectionId = null;
            agent.LastSeenAt = timeProvider.GetUtcNow();

            // Mark any active sessions as crashed since the agent disconnected
            var staleSessions = await db.Sessions
                .AsTracking()
                .Where(s => s.AgentId == agent.Id &&
                    (s.State == "Running" || s.State == "Starting" || s.State == "Requested"))
                .ToListAsync();

            var now = timeProvider.GetUtcNow().UtcDateTime;
            foreach (var session in staleSessions)
            {
                session.State = "Crashed";
                session.EndedAt = now;
            }

            await db.SaveChangesAsync();

            // Notify web clients
            await Clients.Group($"user:{agent.Id}")
                .SendAsync("AgentStatusChanged", agent.Id, false);

            // Notify about crashed sessions
            foreach (var session in staleSessions)
            {
                await Clients.Group($"user:{agent.Id}")
                    .SendAsync("SessionStatusChanged", new SessionStatusUpdate
                    {
                        SessionId = session.Id,
                        AgentId = agent.Id,
                        Path = session.Path,
                        State = Shared.Enums.SessionState.Crashed,
                        StartedAt = session.StartedAt.UtcDateTime,
                        EndedAt = session.EndedAt?.UtcDateTime,
                        ErrorMessage = "Agent disconnected"
                    });
            }

            // Track agent offline for admin summary
            agentTracker.TrackOffline(agent.Id);
            await BroadcastAdminAgentSummaryAsync();
        }

        await base.OnDisconnectedAsync(exception);
    }

    // --- Admin ---

    public async Task SubscribeAsAdmin()
    {
        var auth0UserId = Context.User?.FindFirst("sub")?.Value;
        if (auth0UserId is null) return;

        var isAdmin = await db.Users.AnyAsync(u => u.Auth0UserId == auth0UserId && u.IsAdmin);
        if (!isAdmin) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, "admin");

        var summary = await BuildAdminAgentSummaryAsync();
        await Clients.Caller.SendAsync("AdminAgentSummary", summary);
    }

    private async Task<object> BuildAdminAgentSummaryAsync()
    {
        var onlineCounts = agentTracker.GetOnlineCountsByAccount();

        var accountStats = await db.Agents
            .GroupBy(a => a.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Installed = g.Count(),
                MaxAgents = g.First().Account.Plan != null ? g.First().Account.Plan!.MaxAgents : 0
            })
            .ToListAsync();

        var accounts = new Dictionary<string, object>();
        var globalOnline = 0;
        var globalInstalled = 0;
        var globalMax = 0;

        foreach (var stat in accountStats)
        {
            var online = onlineCounts.GetValueOrDefault(stat.AccountId, 0);
            globalOnline += online;
            globalInstalled += stat.Installed;
            globalMax += stat.MaxAgents;

            accounts[stat.AccountId.ToString()] = new
            {
                online,
                installed = stat.Installed,
                maxAgents = stat.MaxAgents
            };
        }

        return new
        {
            global = new
            {
                online = globalOnline,
                installed = globalInstalled,
                maxAgents = globalMax
            },
            accounts
        };
    }

    private async Task BroadcastAdminAgentSummaryAsync()
    {
        var summary = await BuildAdminAgentSummaryAsync();
        await Clients.Group("admin").SendAsync("AdminAgentSummary", summary);
    }
}
