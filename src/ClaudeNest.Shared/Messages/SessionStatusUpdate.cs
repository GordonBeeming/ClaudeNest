using ClaudeNest.Shared.Enums;

namespace ClaudeNest.Shared.Messages;

public sealed class SessionStatusUpdate
{
    public required Guid SessionId { get; init; }
    public required Guid AgentId { get; init; }
    public required string Path { get; init; }
    public required SessionState State { get; init; }
    public int? Pid { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public int? ExitCode { get; init; }
}
