using System.Collections.Concurrent;

namespace ClaudeNest.Backend.Services;

public class AgentTracker
{
    private readonly ConcurrentDictionary<Guid, (Guid AccountId, string ConnectionId)> _onlineAgents = new();
    private readonly ConcurrentDictionary<Guid, Guid> _activeSessions = new(); // sessionId → accountId

    public void TrackOnline(Guid agentId, Guid accountId, string connectionId)
    {
        _onlineAgents[agentId] = (accountId, connectionId);
    }

    public bool IsTracked(Guid agentId) => _onlineAgents.ContainsKey(agentId);

    /// <summary>
    /// Removes agent from online tracking only if the connectionId matches.
    /// Returns true if the agent was removed, false if the connectionId didn't match
    /// (meaning a newer connection already replaced it).
    /// </summary>
    public bool TrackOffline(Guid agentId, string connectionId)
    {
        if (!_onlineAgents.TryGetValue(agentId, out var current))
            return false;

        if (current.ConnectionId != connectionId)
            return false;

        // Atomic remove: only succeeds if the value still matches
        return ((ICollection<KeyValuePair<Guid, (Guid, string)>>)_onlineAgents)
            .Remove(new KeyValuePair<Guid, (Guid, string)>(agentId, current));
    }

    public Dictionary<Guid, int> GetOnlineCountsByAccount()
    {
        return _onlineAgents
            .GroupBy(kvp => kvp.Value.AccountId)
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
