namespace ClaudeNest.Backend.Data.Entities;

public class CompanyDeal
{
    public Guid Id { get; set; }
    public required string Domain { get; set; }
    public Guid PlanId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeactivatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }

    public Plan Plan { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
