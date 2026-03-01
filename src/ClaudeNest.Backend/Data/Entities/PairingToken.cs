namespace ClaudeNest.Backend.Data.Entities;

public class PairingToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required byte[] TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RedeemedAt { get; set; }

    public User User { get; set; } = null!;
}
