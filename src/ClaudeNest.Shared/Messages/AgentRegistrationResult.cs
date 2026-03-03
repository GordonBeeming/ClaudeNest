namespace ClaudeNest.Shared.Messages;

public sealed class AgentRegistrationResult
{
    public int EffectiveMaxSessions { get; init; }
    public string? LatestAgentVersion { get; init; }
    public string? UpdateDownloadUrl { get; init; }
}
