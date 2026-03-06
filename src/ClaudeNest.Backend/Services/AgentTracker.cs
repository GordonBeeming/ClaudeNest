using System.Collections.Concurrent;

namespace ClaudeNest.Backend.Services;

public class AgentTracker
{
    private readonly ConcurrentDictionary<Guid, Guid> _onlineAgents = new(); // agentId → accountId

    public void TrackOnline(Guid agentId, Guid accountId)
    {
        _onlineAgents[agentId] = accountId;
    }

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
}
