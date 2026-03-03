namespace ClaudeNest.Backend.Data.Entities;

public class UserFolderPreference
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AgentId { get; set; }
    public required string Path { get; set; }
    public bool IsFavorite { get; set; }
    public string? Color { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Agent Agent { get; set; } = null!;
}
