namespace ClaudeNest.Backend.Data.Entities;

public class AgentCredential
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public required byte[] SecretHash { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    public Agent Agent { get; set; } = null!;
}
