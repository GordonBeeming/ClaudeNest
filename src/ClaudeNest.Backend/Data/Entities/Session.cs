namespace ClaudeNest.Backend.Data.Entities;

public class Session
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public required string Path { get; set; }
    public required string State { get; set; }
    public int? Pid { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public int? ExitCode { get; set; }

    public Agent Agent { get; set; } = null!;
}
