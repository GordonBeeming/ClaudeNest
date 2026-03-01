namespace ClaudeNest.Agent.Config;

public sealed class AgentCredentials
{
    public Guid AgentId { get; set; }
    public string Secret { get; set; } = string.Empty;
    public string BackendUrl { get; set; } = string.Empty;
}
