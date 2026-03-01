namespace ClaudeNest.Shared.Messages;

public sealed class DeregisterCommand
{
    public required Guid AgentId { get; init; }
    public string? Reason { get; init; }
}
