namespace ClaudeNest.Backend.Models;

public record UpdateCouponRequest(int? MaxRedemptions, DateTimeOffset? ExpiresAt, bool? IsActive);
