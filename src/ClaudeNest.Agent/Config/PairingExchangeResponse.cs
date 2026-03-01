using System.Text.Json.Serialization;

namespace ClaudeNest.Agent.Config;

public sealed class PairingExchangeResponse
{
    [JsonPropertyName("agentId")]
    public Guid AgentId { get; set; }

    [JsonPropertyName("secret")]
    public string Secret { get; set; } = string.Empty;
}
