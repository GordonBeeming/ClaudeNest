namespace ClaudeNest.Backend.Data.Entities;

public class Agent
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string? Name { get; set; }
    public string? Hostname { get; set; }
    public string? OS { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? AllowedPathsJson { get; set; }

    public Account Account { get; set; } = null!;
    public ICollection<AgentCredential> Credentials { get; set; } = [];
    public ICollection<Session> Sessions { get; set; } = [];
}
