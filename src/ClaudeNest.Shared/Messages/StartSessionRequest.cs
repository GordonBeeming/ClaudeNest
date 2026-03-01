namespace ClaudeNest.Shared.Messages;

public sealed class StartSessionRequest
{
    public required Guid SessionId { get; init; }
    public required string Path { get; init; }
}
