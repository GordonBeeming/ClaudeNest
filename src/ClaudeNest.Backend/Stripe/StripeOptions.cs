namespace ClaudeNest.Backend.Stripe;

public class StripeOptions
{
    public string SecretKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string PublishableKey { get; set; } = "";
    public string SuccessUrl { get; set; } = "https://localhost:5173/plans?success=true";
    public string CancelUrl { get; set; } = "https://localhost:5173/plans?cancelled=true";
    public string BillingPortalReturnUrl { get; set; } = "https://localhost:5173/account";
    public string Currency { get; set; } = "aud";
    public bool TraceWebhooks { get; set; }
    public string TraceWebhooksPath { get; set; } = "";
}
