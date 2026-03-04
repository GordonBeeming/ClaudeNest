namespace ClaudeNest.Shared.Messages;

public sealed class AgentRegistrationResult
{
    public int EffectiveMaxSessions { get; init; }
    public string? LatestAgentVersion { get; init; }
    public string? UpdateDownloadUrl { get; init; }
    public List<ActiveSessionInfo>? ActiveSessions { get; init; }
}

public sealed class ActiveSessionInfo
{
    public required Guid SessionId { get; init; }
    public required string Path { get; init; }
    public int? Pid { get; init; }
}
