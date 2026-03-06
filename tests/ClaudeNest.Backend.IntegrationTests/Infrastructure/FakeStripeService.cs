using System.Text.Json;
using ClaudeNest.Backend.Stripe;
using ClaudeNest.Shared.Enums;

namespace ClaudeNest.Backend.IntegrationTests.Infrastructure;

public class FakeStripeService : IStripeService
{
    public bool IsConfigured => false;

    public List<string> Calls { get; } = [];

    public Task<string> GetOrCreateCustomerAsync(string email, string name, Guid accountId)
    {
        Calls.Add($"GetOrCreateCustomer:{email}");
        return Task.FromResult($"cus_test_{accountId:N}");
    }

    public Task<string> CreateCheckoutSessionAsync(string customerId, string priceId, string? stripeCouponId, string? successUrl, string? cancelUrl)
    {
        Calls.Add($"CreateCheckoutSession:{customerId}:{priceId}");
        return Task.FromResult("https://checkout.stripe.com/test-session");
    }

    public Task<string> CreateBillingPortalSessionAsync(string customerId, string? returnUrl)
    {
        Calls.Add($"CreateBillingPortal:{customerId}");
        return Task.FromResult("https://billing.stripe.com/test-portal");
    }

    public Task<string> CreateStripeCouponAsync(string couponCode, DiscountType discountType, int freeMonths, decimal? percentOff, int? amountOffCents, int? freeDays, int? durationMonths, string currency = "aud")
    {
        Calls.Add($"CreateStripeCoupon:{couponCode}");
        return Task.FromResult($"stripe_coupon_{couponCode}");
    }

    public Task DeactivateStripeCouponAsync(string stripeCouponId)
    {
        Calls.Add($"DeactivateStripeCoupon:{stripeCouponId}");
        return Task.CompletedTask;
    }

    public Task CancelSubscriptionAsync(string subscriptionId)
    {
        Calls.Add($"CancelSubscription:{subscriptionId}");
        return Task.CompletedTask;
    }

    public Task<string?> GetPaymentMethodFingerprintAsync(string checkoutSessionId)
    {
        Calls.Add($"GetPaymentMethodFingerprint:{checkoutSessionId}");
        return Task.FromResult<string?>(null);
    }

    public global::Stripe.Event ConstructWebhookEvent(string json, string signature)
    {
        Calls.Add("ConstructWebhookEvent");

        // Parse the test JSON and construct a proper Stripe Event with typed Data.Object
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var eventType = root.GetProperty("type").GetString()!;
        var eventId = root.GetProperty("id").GetString()!;
        var dataObj = root.GetProperty("data").GetProperty("object");

        var evt = new global::Stripe.Event
        {
            Id = eventId,
            Type = eventType,
            Data = new global::Stripe.EventData()
        };

        evt.Data.Object = eventType switch
        {
            "checkout.session.completed" => BuildCheckoutSession(dataObj),
            "customer.subscription.created" or
            "customer.subscription.updated" or
            "customer.subscription.deleted" => BuildSubscription(dataObj),
            "invoice.paid" or
            "invoice.payment_failed" => BuildInvoice(dataObj),
            _ => new global::Stripe.Account() // Dummy object for unknown events
        };

        return evt;
    }

    private static global::Stripe.Checkout.Session BuildCheckoutSession(JsonElement obj)
    {
        var session = new global::Stripe.Checkout.Session();
        if (obj.TryGetProperty("id", out var id))
            session.Id = id.GetString();
        if (obj.TryGetProperty("customer", out var customer))
            session.CustomerId = customer.GetString();
        if (obj.TryGetProperty("subscription", out var subscription))
            session.SubscriptionId = subscription.GetString();
        if (obj.TryGetProperty("mode", out var mode))
            session.Mode = mode.GetString();
        return session;
    }

    private static global::Stripe.Subscription BuildSubscription(JsonElement obj)
    {
        var sub = new global::Stripe.Subscription();
        if (obj.TryGetProperty("id", out var id))
            sub.Id = id.GetString();
        if (obj.TryGetProperty("customer", out var customer))
            sub.CustomerId = customer.GetString();
        if (obj.TryGetProperty("status", out var status))
            sub.Status = status.GetString();
        if (obj.TryGetProperty("cancel_at_period_end", out var cancelProp))
            sub.CancelAtPeriodEnd = cancelProp.GetBoolean();
        if (obj.TryGetProperty("items", out var items) &&
            items.TryGetProperty("data", out var itemsData))
        {
            sub.Items = new global::Stripe.StripeList<global::Stripe.SubscriptionItem>
            {
                Data = []
            };
            foreach (var item in itemsData.EnumerateArray())
            {
                var subItem = new global::Stripe.SubscriptionItem();
                if (item.TryGetProperty("current_period_end", out var periodEnd))
                {
                    subItem.CurrentPeriodEnd = DateTimeOffset.FromUnixTimeSeconds(periodEnd.GetInt64()).UtcDateTime;
                }
                sub.Items.Data.Add(subItem);
            }
        }
        return sub;
    }

    private static global::Stripe.Invoice BuildInvoice(JsonElement obj)
    {
        var invoice = new global::Stripe.Invoice();
        if (obj.TryGetProperty("id", out var id))
            invoice.Id = id.GetString();
        if (obj.TryGetProperty("customer", out var customer))
            invoice.CustomerId = customer.GetString();
        if (obj.TryGetProperty("amount_due", out var amountDue))
            invoice.AmountDue = amountDue.GetInt64();
        if (obj.TryGetProperty("amount_paid", out var amountPaid))
            invoice.AmountPaid = amountPaid.GetInt64();
        if (obj.TryGetProperty("number", out var number))
            invoice.Number = number.GetString();
        return invoice;
    }

    public Task<string> GetOrCreatePriceAsync(string planName, int priceCents, string currency)
    {
        Calls.Add($"GetOrCreatePrice:{planName}");
        return Task.FromResult($"price_test_{planName}");
    }
}
