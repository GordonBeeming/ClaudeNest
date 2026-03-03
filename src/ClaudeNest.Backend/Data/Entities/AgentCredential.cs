namespace ClaudeNest.Backend.Data.Entities;

public class AgentCredential
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public required byte[] SecretHash { get; set; }
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }

    public Agent Agent { get; set; } = null!;
}
