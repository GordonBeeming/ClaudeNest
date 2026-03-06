using System.Text.Json;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Data.Entities;
using ClaudeNest.Backend.Stripe;
using ClaudeNest.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;

namespace ClaudeNest.Backend.Controllers;

[Route("api/stripe/webhook")]
[AllowAnonymous]
public class StripeWebhookController(NestDbContext db, IStripeService stripeService, IOptions<StripeOptions> stripeOptions, ILogger<StripeWebhookController> logger, TimeProvider timeProvider) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        Request.EnableBuffering();
        string json;
        using (var reader = new StreamReader(Request.Body))
        {
            json = await reader.ReadToEndAsync();
        }

        // Trace: write raw webhook payload to disk for debugging
        var opts = stripeOptions.Value;
        if (opts.TraceWebhooks)
        {
            try
            {
                var traceDir = !string.IsNullOrEmpty(opts.TraceWebhooksPath)
                    ? opts.TraceWebhooksPath
                    : Path.Combine(Path.GetTempPath(), "claudenest-webhook-traces");
                Directory.CreateDirectory(traceDir);

                // Extract event type from JSON for the filename
                var eventType = "unknown";
                var eventId = "unknown";
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    eventType = doc.RootElement.GetProperty("type").GetString()?.Replace(".", "_") ?? "unknown";
                    eventId = doc.RootElement.GetProperty("id").GetString() ?? "unknown";
                }
                catch { /* use defaults */ }

                var filename = $"{timeProvider.GetUtcNow():yyyyMMdd_HHmmss}_{eventType}_{eventId}.json";
                await System.IO.File.WriteAllTextAsync(Path.Combine(traceDir, filename), json);
                logger.LogInformation("Webhook trace written: {TraceFile}", filename);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to write webhook trace file");
            }
        }

        Event stripeEvent;

        if (string.IsNullOrEmpty(stripeOptions.Value.WebhookSecret))
        {
            // No webhook secret configured — parse event without signature verification (dev only)
            logger.LogWarning("Stripe webhook secret not configured — skipping signature verification");
            try
            {
                stripeEvent = EventUtility.ParseEvent(json, false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse Stripe webhook event");
                return BadRequest("Invalid event payload");
            }
        }
        else
        {
            var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();
            if (string.IsNullOrEmpty(signature))
                return BadRequest("Missing Stripe-Signature header");

            try
            {
                stripeEvent = stripeService.ConstructWebhookEvent(json, signature);
            }
            catch (StripeException)
            {
                return BadRequest("Invalid webhook signature");
            }
        }

        logger.LogInformation("Processing Stripe webhook event: {EventType} [{EventId}]", stripeEvent.Type, stripeEvent.Id);

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await HandleCheckoutSessionCompleted(stripeEvent);
                break;
            case "customer.subscription.created":
            case "customer.subscription.updated":
                await HandleSubscriptionUpdated(stripeEvent);
                break;
            case "customer.subscription.deleted":
                await HandleSubscriptionDeleted(stripeEvent);
                break;
            case "invoice.paid":
                await HandleInvoicePaid(stripeEvent);
                break;
            case "invoice.payment_failed":
                await HandleInvoicePaymentFailed(stripeEvent);
                break;
        }

        return Ok();
    }

    private async Task HandleCheckoutSessionCompleted(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as global::Stripe.Checkout.Session;
        if (session is null) return;

        var customerId = session.CustomerId;
        if (customerId is null) return;

        var account = await db.Accounts.AsTracking().FirstOrDefaultAsync(a => a.StripeCustomerId == customerId);
        if (account is null) return;

        var subscriptionId = session.SubscriptionId;
        if (!string.IsNullOrEmpty(subscriptionId))
            account.StripeSubscriptionId = subscriptionId;

        account.SubscriptionStatus = SubscriptionStatus.Active;

        // Get payment method fingerprint
        if (session.SetupIntentId is not null)
        {
            try
            {
                var fingerprint = await stripeService.GetPaymentMethodFingerprintAsync(session.Id);
                if (fingerprint is not null)
                    account.StripePaymentMethodFingerprint = fingerprint;
            }
            catch
            {
                // Non-critical, continue
            }
        }

        // Handle coupon redemption if metadata present
        if (session.Metadata?.TryGetValue("couponId", out var couponIdStr) == true &&
            Guid.TryParse(couponIdStr, out var couponId))
        {
            var coupon = await db.Coupons.AsTracking().FirstOrDefaultAsync(c => c.Id == couponId);
            if (coupon is not null)
            {
                var alreadyRedeemed = await db.CouponRedemptions
                    .AnyAsync(cr => cr.CouponId == couponId && cr.AccountId == account.Id);

                if (!alreadyRedeemed)
                {
                    var redemptionNow = timeProvider.GetUtcNow();
                    var freeUntil = coupon.DiscountType switch
                    {
                        Shared.Enums.DiscountType.FreeDays => redemptionNow.AddDays(coupon.FreeDays ?? 0),
                        Shared.Enums.DiscountType.PercentOff or Shared.Enums.DiscountType.AmountOff => redemptionNow.AddMonths(coupon.DurationMonths),
                        _ => redemptionNow.AddMonths(coupon.FreeMonths)
                    };
                    db.CouponRedemptions.Add(new CouponRedemption
                    {
                        CouponId = couponId,
                        AccountId = account.Id,
                        FreeUntil = freeUntil,
                        StripeCheckoutSessionId = session.Id
                    });
                    coupon.TimesRedeemed++;
                }
            }
        }

        // Ledger entries are created by the invoice.paid handler — not here

        await db.SaveChangesAsync();
    }

    private async Task HandleSubscriptionUpdated(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription is null) return;

        var subscriptionId = subscription.Id;
        var account = await db.Accounts.AsTracking().FirstOrDefaultAsync(a => a.StripeSubscriptionId == subscriptionId);

        // For subscription.created, the account won't have StripeSubscriptionId yet — look up by customer
        if (account is null)
        {
            var customerId = subscription.CustomerId;
            if (customerId is null) return;
            account = await db.Accounts.AsTracking().FirstOrDefaultAsync(a => a.StripeCustomerId == customerId);
            if (account is null) return;

            account.StripeSubscriptionId = subscriptionId;
        }

        account.SubscriptionStatus = subscription.Status switch
        {
            "active" => SubscriptionStatus.Active,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" or "unpaid" => SubscriptionStatus.Cancelled,
            "trialing" => SubscriptionStatus.Trialing,
            _ => account.SubscriptionStatus
        };

        var periodEnd = subscription.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd;
        if (periodEnd is not null && periodEnd != default)
            account.CurrentPeriodEnd = periodEnd;

        account.CancelAtPeriodEnd = subscription.CancelAtPeriodEnd;

        await db.SaveChangesAsync();
    }

    private async Task HandleSubscriptionDeleted(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription is null) return;

        var subscriptionId = subscription.Id;
        var account = await db.Accounts.AsTracking().FirstOrDefaultAsync(a => a.StripeSubscriptionId == subscriptionId);
        if (account is null) return;

        account.SubscriptionStatus = SubscriptionStatus.Cancelled;
        account.CancelAtPeriodEnd = false;
        await db.SaveChangesAsync();
    }

    private async Task HandleInvoicePaid(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice is null) return;

        var customerId = invoice.CustomerId;
        if (customerId is null) return;

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.StripeCustomerId == customerId);
        if (account is null) return;

        // Idempotency check
        var invoiceId = invoice.Id;
        var alreadyRecorded = await db.AccountLedger
            .AnyAsync(e => e.StripeInvoiceId == invoiceId && e.EntryType == LedgerEntryType.PaymentDue);

        if (!alreadyRecorded)
        {
            var invoiceLabel = invoice.Number ?? invoice.Id;

            // PaymentDue entry (what was billed)
            db.AccountLedger.Add(new AccountLedger
            {
                AccountId = account.Id,
                EntryType = LedgerEntryType.PaymentDue,
                AmountCents = -(int)invoice.AmountDue,
                Description = $"Invoice {invoiceLabel}",
                StripeInvoiceId = invoice.Id,
                PlanId = account.PlanId
            });

            if (invoice.AmountPaid > 0)
            {
                // PaymentReceived entry
                db.AccountLedger.Add(new AccountLedger
                {
                    AccountId = account.Id,
                    EntryType = LedgerEntryType.PaymentReceived,
                    AmountCents = (int)invoice.AmountPaid,
                    Description = $"Payment for invoice {invoiceLabel}",
                    StripeInvoiceId = invoice.Id,
                    PlanId = account.PlanId
                });
            }
            else if (invoice.Discounts?.Count > 0)
            {
                // CouponCredit entry when fully covered by coupon
                db.AccountLedger.Add(new AccountLedger
                {
                    AccountId = account.Id,
                    EntryType = LedgerEntryType.CouponCredit,
                    AmountCents = (int)invoice.AmountDue,
                    Description = $"Coupon credit for invoice {invoiceLabel}",
                    StripeInvoiceId = invoice.Id,
                    PlanId = account.PlanId
                });
            }

            await db.SaveChangesAsync();
        }
    }

    private async Task HandleInvoicePaymentFailed(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice is null) return;

        var customerId = invoice.CustomerId;
        if (customerId is null) return;

        var account = await db.Accounts.AsTracking().FirstOrDefaultAsync(a => a.StripeCustomerId == customerId);
        if (account is null) return;

        account.SubscriptionStatus = SubscriptionStatus.PastDue;
        await db.SaveChangesAsync();
    }
}
