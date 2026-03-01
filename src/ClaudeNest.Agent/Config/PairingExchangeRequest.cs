namespace ClaudeNest.Agent.Config;

public sealed class PairingExchangeRequest
{
    public string Token { get; set; } = string.Empty;
    public string? AgentName { get; set; }
    public string? Hostname { get; set; }
    public string? OS { get; set; }
}
