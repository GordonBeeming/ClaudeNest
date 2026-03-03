using ClaudeNest.Shared.Enums;

namespace ClaudeNest.Backend.Data.Entities;

public class AccountLedger
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public LedgerEntryType EntryType { get; set; }
    public int AmountCents { get; set; }
    public string Currency { get; set; } = "aud";
    public required string Description { get; set; }
    public Guid? PlanId { get; set; }
    public string? StripeInvoiceId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public Guid? CouponId { get; set; }
    public Guid? CompanyDealId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Account Account { get; set; } = null!;
    public Plan? Plan { get; set; }
    public Coupon? Coupon { get; set; }
    public CompanyDeal? CompanyDeal { get; set; }
}
