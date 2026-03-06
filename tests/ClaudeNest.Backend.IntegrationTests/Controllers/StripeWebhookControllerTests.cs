using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.IntegrationTests.Infrastructure;
using ClaudeNest.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeNest.Backend.IntegrationTests.Controllers;

public class StripeWebhookControllerTests(ClaudeNestWebApplicationFactory factory) : IClassFixture<ClaudeNestWebApplicationFactory>
{
    private static string BuildWebhookPayload(string type, string objectType, object dataObject)
    {
        return JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.NewGuid():N}",
            @object = "event",
            type,
            data = new
            {
                @object = dataObject
            }
        });
    }

    private static HttpRequestMessage CreateWebhookRequest(string json)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/stripe/webhook")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        // Add a dummy signature header - FakeStripeService.ConstructWebhookEvent ignores it
        request.Headers.Add("Stripe-Signature", "t=1234567890,v1=fakesig");
        return request;
    }

    [Fact]
    public async Task HandleWebhook_CheckoutSessionCompleted_ActivatesSubscription()
    {
        var user = new TestUser("auth0|sw-checkout", "sw-checkout@test.com", "SW Checkout");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user, subscriptionStatus: SubscriptionStatus.None);

        // Set Stripe customer ID on the account
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var acct = await db.Accounts.AsTracking().FirstAsync(a => a.Id == account.Id);
            acct.StripeCustomerId = "cus_checkout_test";
            await db.SaveChangesAsync();
        }

        var json = BuildWebhookPayload("checkout.session.completed", "checkout.session", new
        {
            @object = "checkout.session",
            id = "cs_checkout_test",
            customer = "cus_checkout_test",
            subscription = "sub_checkout_test",
            mode = "subscription"
        });

        var client = factory.CreateClient();
        var response = await client.SendAsync(CreateWebhookRequest(json));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<NestDbContext>();
        var acctVerify = await verifyDb.Accounts.FirstAsync(a => a.Id == account.Id);
        Assert.Equal(SubscriptionStatus.Active, acctVerify.SubscriptionStatus);
    }

    [Fact]
    public async Task HandleWebhook_SubscriptionUpdated_UpdatesStatus()
    {
        var user = new TestUser("auth0|sw-sub-updated", "sw-sub-updated@test.com", "SW SubUpdated");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var acct = await db.Accounts.AsTracking().FirstAsync(a => a.Id == account.Id);
            acct.StripeCustomerId = "cus_sub_updated";
            acct.StripeSubscriptionId = "sub_updated_test";
            await db.SaveChangesAsync();
        }

        var json = BuildWebhookPayload("customer.subscription.updated", "subscription", new
        {
            @object = "subscription",
            id = "sub_updated_test",
            customer = "cus_sub_updated",
            status = "past_due",
            cancel_at_period_end = true,
            items = new { data = new[] { new { current_period_end = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds() } } }
        });

        var client = factory.CreateClient();
        var response = await client.SendAsync(CreateWebhookRequest(json));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<NestDbContext>();
        var acctVerify = await verifyDb.Accounts.FirstAsync(a => a.Id == account.Id);
        Assert.Equal(SubscriptionStatus.PastDue, acctVerify.SubscriptionStatus);
        Assert.True(acctVerify.CancelAtPeriodEnd);
    }

    [Fact]
    public async Task HandleWebhook_SubscriptionDeleted_CancelsAccount()
    {
        var user = new TestUser("auth0|sw-sub-deleted", "sw-sub-deleted@test.com", "SW SubDeleted");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var acct = await db.Accounts.AsTracking().FirstAsync(a => a.Id == account.Id);
            acct.StripeSubscriptionId = "sub_deleted_test";
            await db.SaveChangesAsync();
        }

        var json = BuildWebhookPayload("customer.subscription.deleted", "subscription", new
        {
            @object = "subscription",
            id = "sub_deleted_test",
            status = "canceled"
        });

        var client = factory.CreateClient();
        var response = await client.SendAsync(CreateWebhookRequest(json));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<NestDbContext>();
        var acctVerify = await verifyDb.Accounts.FirstAsync(a => a.Id == account.Id);
        Assert.Equal(SubscriptionStatus.Cancelled, acctVerify.SubscriptionStatus);
    }

    [Fact]
    public async Task HandleWebhook_InvoicePaid_CreatesLedgerEntries()
    {
        var user = new TestUser("auth0|sw-inv-paid", "sw-inv-paid@test.com", "SW InvPaid");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var acct = await db.Accounts.AsTracking().FirstAsync(a => a.Id == account.Id);
            acct.StripeCustomerId = "cus_inv_paid";
            await db.SaveChangesAsync();
        }

        var invoiceId = $"in_{Guid.NewGuid():N}";
        var json = BuildWebhookPayload("invoice.paid", "invoice", new
        {
            @object = "invoice",
            id = invoiceId,
            customer = "cus_inv_paid",
            amount_due = 500,
            amount_paid = 500,
            number = "INV-001"
        });

        var client = factory.CreateClient();
        var response = await client.SendAsync(CreateWebhookRequest(json));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<NestDbContext>();
        var entries = await verifyDb.AccountLedger.Where(e => e.StripeInvoiceId == invoiceId).ToListAsync();
        Assert.True(entries.Count >= 2); // PaymentDue + PaymentReceived
    }

    [Fact]
    public async Task HandleWebhook_InvoicePaid_IdempotencyCheck()
    {
        var user = new TestUser("auth0|sw-inv-idemp", "sw-inv-idemp@test.com", "SW InvIdemp");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var acct = await db.Accounts.AsTracking().FirstAsync(a => a.Id == account.Id);
            acct.StripeCustomerId = "cus_inv_idemp";
            await db.SaveChangesAsync();
        }

        var invoiceId = $"in_{Guid.NewGuid():N}";
        var json = BuildWebhookPayload("invoice.paid", "invoice", new
        {
            @object = "invoice",
            id = invoiceId,
            customer = "cus_inv_idemp",
            amount_due = 500,
            amount_paid = 500,
            number = "INV-IDEMP"
        });

        var client = factory.CreateClient();

        // Send twice
        await client.SendAsync(CreateWebhookRequest(json));
        // Need a new request object for the second call
        await client.SendAsync(CreateWebhookRequest(json));

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<NestDbContext>();
        var dueEntries = await verifyDb.AccountLedger
            .Where(e => e.StripeInvoiceId == invoiceId && e.EntryType == LedgerEntryType.PaymentDue)
            .CountAsync();
        Assert.Equal(1, dueEntries); // Only one due entry despite two webhook calls
    }

    [Fact]
    public async Task HandleWebhook_InvoicePaymentFailed_SetsPastDue()
    {
        var user = new TestUser("auth0|sw-inv-failed", "sw-inv-failed@test.com", "SW InvFailed");
        var (_, account) = await TestDatabaseHelper.SeedUserAsync(factory.Services, user);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
            var acct = await db.Accounts.AsTracking().FirstAsync(a => a.Id == account.Id);
            acct.StripeCustomerId = "cus_inv_failed";
            await db.SaveChangesAsync();
        }

        var json = BuildWebhookPayload("invoice.payment_failed", "invoice", new
        {
            @object = "invoice",
            id = $"in_{Guid.NewGuid():N}",
            customer = "cus_inv_failed"
        });

        var client = factory.CreateClient();
        var response = await client.SendAsync(CreateWebhookRequest(json));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<NestDbContext>();
        var acctVerify = await verifyDb.Accounts.FirstAsync(a => a.Id == account.Id);
        Assert.Equal(SubscriptionStatus.PastDue, acctVerify.SubscriptionStatus);
    }

    [Fact]
    public async Task HandleWebhook_UnknownEvent_Returns200()
    {
        var json = BuildWebhookPayload("unknown.event.type", "unknown", new { @object = "unknown" });

        var client = factory.CreateClient();
        var response = await client.SendAsync(CreateWebhookRequest(json));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
