namespace ClaudeNest.Shared.Messages;

public sealed class StopSessionRequest
{
    public required Guid SessionId { get; init; }
}
