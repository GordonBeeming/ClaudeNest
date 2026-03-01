namespace ClaudeNest.Backend.Data.Entities;

public class Plan
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int MaxAgents { get; set; }
    public int MaxSessions { get; set; }
    public int PriceCents { get; set; }
    public int TrialDays { get; set; }
    public string? StripeProductId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
