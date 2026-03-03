namespace ClaudeNest.Backend.Data.Entities;

public class Session
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public required string Path { get; set; }
    public required string State { get; set; }
    public int? Pid { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
    public int? ExitCode { get; set; }

    public Agent Agent { get; set; } = null!;
}
