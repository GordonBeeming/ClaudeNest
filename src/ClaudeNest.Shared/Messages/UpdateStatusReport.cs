namespace ClaudeNest.Shared.Messages;

public sealed class UpdateStatusReport
{
    public required Guid AgentId { get; init; }
    public required string Status { get; init; } // "downloading", "downloaded", "replacing", "restarting", "completed", "failed"
    public string? Error { get; init; }
    public string? NewVersion { get; init; }
}
