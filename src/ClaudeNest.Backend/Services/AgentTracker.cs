using System.Collections.Concurrent;

namespace ClaudeNest.Backend.Services;

public class AgentTracker
{
    private readonly ConcurrentDictionary<Guid, Guid> _onlineAgents = new(); // agentId → accountId
    private readonly ConcurrentDictionary<Guid, Guid> _activeSessions = new(); // sessionId → accountId

    public void TrackOnline(Guid agentId, Guid accountId)
    {
        _onlineAgents[agentId] = accountId;
    }

    public bool IsTracked(Guid agentId) => _onlineAgents.ContainsKey(agentId);

    public void TrackOffline(Guid agentId)
    {
        _onlineAgents.TryRemove(agentId, out _);
    }

    public Dictionary<Guid, int> GetOnlineCountsByAccount()
    {
        return _onlineAgents
            .GroupBy(kvp => kvp.Value)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public void TrackSessionActive(Guid sessionId, Guid accountId)
    {
        _activeSessions[sessionId] = accountId;
    }

    public void TrackSessionInactive(Guid sessionId)
    {
        _activeSessions.TryRemove(sessionId, out _);
    }

    public void RemoveSessionsForAccount(Guid accountId, IEnumerable<Guid> sessionIds)
    {
        foreach (var sessionId in sessionIds)
        {
            _activeSessions.TryRemove(sessionId, out _);
        }
    }

    public Dictionary<Guid, int> GetActiveSessionCountsByAccount()
    {
        return _activeSessions
            .GroupBy(kvp => kvp.Value)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public int GetGlobalOnlineAgentCount() => _onlineAgents.Count;

    public int GetGlobalActiveSessionCount() => _activeSessions.Count;
}
