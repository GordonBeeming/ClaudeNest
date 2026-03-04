using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeNest.Backend.Auth;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Hubs;
using ClaudeNest.Backend.Stripe;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// User secrets are only loaded automatically in Development.
// Load them explicitly for ProdLike so Auth0 config is available.
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsProduction())
{
    builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
}

builder.AddServiceDefaults();

// Database
builder.AddSqlServerDbContext<NestDbContext>("nestdb", configureDbContextOptions: options =>
{
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

// Authentication — dev bypass or Auth0 JWT
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication(DevAuthHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(DevAuthHandler.SchemeName, _ => { });
}
else
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Auth0:Authority"];
            options.Audience = builder.Configuration["Auth0:Audience"];
            options.MapInboundClaims = false;
        });
}
builder.Services.AddAuthorization();

// Time
builder.Services.AddSingleton(TimeProvider.System);

// SignalR — use camelCase + string enums to match frontend expectations
var signalRBuilder = builder.Services.AddSignalR(options =>
    {
        if (builder.Environment.IsDevelopment())
            options.EnableDetailedErrors = true;
    })
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Use Azure SignalR Service when configured
if (!string.IsNullOrEmpty(builder.Configuration["Azure:SignalR:ConnectionString"]))
{
    signalRBuilder.AddAzureSignalR();
}

// Stripe
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection("Stripe"));
builder.Services.AddScoped<IStripeService, StripeService>();
var stripeKey = builder.Configuration["Stripe:SecretKey"];
if (!string.IsNullOrEmpty(stripeKey))
{
    global::Stripe.StripeConfiguration.ApiKey = stripeKey;
}

// HTTP client for Auth0 /userinfo calls
builder.Services.AddHttpClient();

// Data Protection — persist keys to database
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<NestDbContext>();

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// CORS for web frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Database initialization
if (app.Environment.IsDevelopment())
{
    // Dev mode: auto-create DB + seed test data
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
    await DevDataSeeder.SeedAsync(db);
    // Reset all agents to offline on startup (stale connections from previous run)
    await db.Agents.Where(a => a.IsOnline).ExecuteUpdateAsync(s => s
        .SetProperty(a => a.IsOnline, false)
        .SetProperty(a => a.ConnectionId, (string?)null));
    // Clean up stale sessions left in active states from previous run
    await db.Sessions
        .Where(s => s.State == "Running" || s.State == "Starting" || s.State == "Requested")
        .ExecuteUpdateAsync(s => s
            .SetProperty(s => s.State, "Crashed")
            .SetProperty(s => s.EndedAt, DateTime.UtcNow));
}
else
{
    // ProdLike / Production: apply EF migrations
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
    await db.Database.MigrateAsync();
    // Reset all agents to offline on startup (stale connections from previous run)
    await db.Agents.Where(a => a.IsOnline).ExecuteUpdateAsync(s => s
        .SetProperty(a => a.IsOnline, false)
        .SetProperty(a => a.ConnectionId, (string?)null));
    // Clean up stale sessions left in active states from previous run
    await db.Sessions
        .Where(s => s.State == "Running" || s.State == "Starting" || s.State == "Requested")
        .ExecuteUpdateAsync(s => s
            .SetProperty(s => s.State, "Crashed")
            .SetProperty(s => s.EndedAt, DateTime.UtcNow));
}

// Sync plans to Stripe if configured (creates products + prices)
{
    using var stripeScope = app.Services.CreateScope();
    var stripeSvc = stripeScope.ServiceProvider.GetRequiredService<IStripeService>();
    if (stripeSvc.IsConfigured)
    {
        var stripeDb = stripeScope.ServiceProvider.GetRequiredService<NestDbContext>();
        var stripeOpts = stripeScope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<StripeOptions>>().Value;
        var plans = await stripeDb.Plans.AsTracking().Where(p => p.IsActive && p.StripePriceId == null).ToListAsync();
        foreach (var plan in plans)
        {
            try
            {
                var priceId = await stripeSvc.GetOrCreatePriceAsync(plan.Name, plan.PriceCents, stripeOpts.Currency);
                plan.StripePriceId = priceId;
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "Failed to sync plan {PlanName} to Stripe", plan.Name);
            }
        }
        if (plans.Count > 0)
            await stripeDb.SaveChangesAsync();
    }
}

app.MapDefaultEndpoints();

// Forward headers from reverse proxy (Cloudflare Tunnel / Azure Container Apps)
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
};
// Trust all proxies — traffic arrives via Cloudflare Tunnel on a private VNet
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<AgentAuthMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NestHub>("/hubs/nest");

app.Run();
