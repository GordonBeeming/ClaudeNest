using ClaudeNest.Shared.Enums;

namespace ClaudeNest.Backend.Stripe;

public interface IStripeService
{
    Task<string> GetOrCreateCustomerAsync(string email, string name, Guid accountId);
    Task<string> CreateCheckoutSessionAsync(string customerId, string priceId, string? stripeCouponId, string? successUrl, string? cancelUrl);
    Task<string> CreateBillingPortalSessionAsync(string customerId, string? returnUrl);
    Task<string> CreateStripeCouponAsync(string couponCode, DiscountType discountType, int freeMonths, decimal? percentOff, int? amountOffCents, int? freeDays, int? durationMonths, string currency = "aud");
    Task DeactivateStripeCouponAsync(string stripeCouponId);
    Task CancelSubscriptionAsync(string subscriptionId);
    Task<string?> GetPaymentMethodFingerprintAsync(string checkoutSessionId);
    global::Stripe.Event ConstructWebhookEvent(string json, string signature);
    Task<string> GetOrCreatePriceAsync(string planName, int priceCents, string currency);
    bool IsConfigured { get; }
}
