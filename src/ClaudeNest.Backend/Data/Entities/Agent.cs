namespace ClaudeNest.Backend.Data.Entities;

public class Agent
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string? Name { get; set; }
    public string? Hostname { get; set; }
    public string? OS { get; set; }
    public bool IsOnline { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? AllowedPathsJson { get; set; }
    public string? Version { get; set; }
    public string? Architecture { get; set; }
    public string? ConnectionId { get; set; }

    public Account Account { get; set; } = null!;
    public ICollection<AgentCredential> Credentials { get; set; } = [];
    public ICollection<Session> Sessions { get; set; } = [];
}
