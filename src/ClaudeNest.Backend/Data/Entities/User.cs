namespace ClaudeNest.Backend.Data.Entities;

public class User
{
    public Guid Id { get; set; }
    public required string Auth0UserId { get; set; }
    public required string Email { get; set; }
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Agent> Agents { get; set; } = [];
    public ICollection<PairingToken> PairingTokens { get; set; } = [];
}
