namespace ClaudeNest.Shared.Messages;

public sealed class AgentInfo
{
    public required Guid AgentId { get; init; }
    public string? Name { get; init; }
    public required string Hostname { get; init; }
    public required string OS { get; init; }
    public string? Version { get; init; }
    public string? Architecture { get; init; }
    public List<string> AllowedPaths { get; init; } = [];
}
