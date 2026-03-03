using ClaudeNest.Shared.Enums;
using Microsoft.Extensions.Options;
using Stripe;

namespace ClaudeNest.Backend.Stripe;

public class StripeService(IOptions<StripeOptions> options) : IStripeService
{
    private readonly StripeOptions _options = options.Value;

    private StripeClient GetClient() => new(_options.SecretKey);

    public async Task<string> GetOrCreateCustomerAsync(string email, string name, Guid accountId)
    {
        var client = GetClient();

        var searchOptions = new CustomerSearchOptions
        {
            Query = $"metadata['accountId']:'{accountId}'"
        };
        var searchResult = await client.V1.Customers.SearchAsync(searchOptions);

        if (searchResult.Data.Count > 0)
            return searchResult.Data[0].Id;

        var createOptions = new CustomerCreateOptions
        {
            Email = email,
            Name = name,
            Metadata = new Dictionary<string, string>
            {
                ["accountId"] = accountId.ToString()
            }
        };
        var customer = await client.V1.Customers.CreateAsync(createOptions);
        return customer.Id;
    }

    public async Task<string> CreateCheckoutSessionAsync(
        string customerId, string priceId, string? stripeCouponId,
        string? successUrl, string? cancelUrl)
    {
        var client = GetClient();

        var sessionOptions = new global::Stripe.Checkout.SessionCreateOptions
        {
            Customer = customerId,
            Mode = "subscription",
            PaymentMethodCollection = "always",
            LineItems =
            [
                new global::Stripe.Checkout.SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1
                }
            ],
            SuccessUrl = successUrl ?? _options.SuccessUrl,
            CancelUrl = cancelUrl ?? _options.CancelUrl
        };

        if (!string.IsNullOrEmpty(stripeCouponId))
        {
            sessionOptions.Discounts =
            [
                new global::Stripe.Checkout.SessionDiscountOptions
                {
                    Coupon = stripeCouponId
                }
            ];
        }

        var session = await client.V1.Checkout.Sessions.CreateAsync(sessionOptions);
        return session.Url;
    }

    public async Task<string> CreateBillingPortalSessionAsync(string customerId, string? returnUrl)
    {
        var client = GetClient();

        var portalOptions = new global::Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl ?? _options.BillingPortalReturnUrl
        };

        var session = await client.V1.BillingPortal.Sessions.CreateAsync(portalOptions);
        return session.Url;
    }

    public async Task<string> CreateStripeCouponAsync(
        string couponCode, DiscountType discountType, int freeMonths,
        decimal? percentOff, int? amountOffCents, int? freeDays,
        int? durationMonths, string currency = "aud")
    {
        var client = GetClient();

        var couponOptions = new CouponCreateOptions
        {
            Id = couponCode,
        };

        switch (discountType)
        {
            case DiscountType.FreeMonths:
                couponOptions.PercentOff = 100m;
                couponOptions.Duration = "repeating";
                couponOptions.DurationInMonths = freeMonths;
                break;
            case DiscountType.PercentOff:
                couponOptions.PercentOff = percentOff ?? 0m;
                couponOptions.Duration = "repeating";
                couponOptions.DurationInMonths = durationMonths ?? 1;
                break;
            case DiscountType.AmountOff:
                couponOptions.AmountOff = amountOffCents ?? 0;
                couponOptions.Currency = currency;
                couponOptions.Duration = "repeating";
                couponOptions.DurationInMonths = durationMonths ?? 1;
                break;
            case DiscountType.FreeDays:
                couponOptions.PercentOff = 100m;
                couponOptions.Duration = "once";
                break;
        }

        var coupon = await client.V1.Coupons.CreateAsync(couponOptions);
        return coupon.Id;
    }

    public async Task DeactivateStripeCouponAsync(string stripeCouponId)
    {
        var client = GetClient();
        await client.V1.Coupons.DeleteAsync(stripeCouponId);
    }

    public async Task CancelSubscriptionAsync(string subscriptionId)
    {
        var client = GetClient();
        await client.V1.Subscriptions.CancelAsync(subscriptionId);
    }

    public async Task<string?> GetPaymentMethodFingerprintAsync(string checkoutSessionId)
    {
        var client = GetClient();

        var session = await client.V1.Checkout.Sessions.GetAsync(checkoutSessionId, new global::Stripe.Checkout.SessionGetOptions
        {
            Expand = ["setup_intent.payment_method"]
        });

        var setupIntent = session.SetupIntent;
        if (setupIntent is null) return null;

        var paymentMethod = setupIntent.PaymentMethod;
        return paymentMethod?.Card?.Fingerprint;
    }

    public Event ConstructWebhookEvent(string json, string signature)
    {
        return EventUtility.ConstructEvent(json, signature, _options.WebhookSecret, throwOnApiVersionMismatch: false);
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_options.SecretKey);

    public async Task<string> GetOrCreatePriceAsync(string planName, int priceCents, string currency)
    {
        var client = GetClient();

        // Search for existing product by metadata
        var products = await client.V1.Products.SearchAsync(new ProductSearchOptions
        {
            Query = $"metadata['claudenest_plan']:'{planName}'"
        });

        string productId;
        if (products.Data.Count > 0)
        {
            productId = products.Data[0].Id;

            // Check if there's already a matching active price
            var existingPrices = await client.V1.Prices.ListAsync(new PriceListOptions
            {
                Product = productId,
                Active = true,
                Limit = 10
            });

            var matchingPrice = existingPrices.Data
                .FirstOrDefault(p => p.UnitAmount == priceCents && p.Currency == currency && p.Recurring?.Interval == "month");

            if (matchingPrice is not null)
                return matchingPrice.Id;
        }
        else
        {
            var product = await client.V1.Products.CreateAsync(new ProductCreateOptions
            {
                Name = $"ClaudeNest {planName}",
                Metadata = new Dictionary<string, string> { ["claudenest_plan"] = planName }
            });
            productId = product.Id;
        }

        var price = await client.V1.Prices.CreateAsync(new PriceCreateOptions
        {
            Product = productId,
            UnitAmount = priceCents,
            Currency = currency,
            Recurring = new PriceRecurringOptions { Interval = "month" }
        });

        return price.Id;
    }
}
