namespace ClaudeNest.Backend.Models;

public record SelectPlanRequest(Guid PlanId, string? CouponCode = null);
