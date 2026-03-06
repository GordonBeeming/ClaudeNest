using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Data.Entities;
using ClaudeNest.Backend.Stripe;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MsSql;

namespace ClaudeNest.Backend.IntegrationTests.Infrastructure;

public class ClaudeNestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public FakeStripeService FakeStripe { get; } = new();

    public static readonly Guid WrenPlanId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid RobinPlanId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid HawkPlanId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid EaglePlanId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    public static readonly Guid FalconPlanId = Guid.Parse("10000000-0000-0000-0000-000000000005");
    public static readonly Guid CondorPlanId = Guid.Parse("10000000-0000-0000-0000-000000000006");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Copy scripts from the Backend's output directory to the content root
        // so InstallScriptController can find them
        EnsureScriptsAvailable();

        builder.ConfigureServices(services =>
        {
            // Remove all EF Core / NestDbContext registrations (including Aspire's)
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<NestDbContext>) ||
                    d.ServiceType == typeof(NestDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition().Name.Contains("IDbContextOptionsConfiguration")) ||
                    (d.ServiceType.FullName?.Contains("NestDbContext") == true &&
                     d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<DbContextOptions<NestDbContext>>();

            services.AddDbContext<NestDbContext>(options =>
            {
                options.UseSqlServer(_msSqlContainer.GetConnectionString());
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            });

            // Replace Stripe service
            services.RemoveAll<IStripeService>();
            services.AddSingleton<IStripeService>(FakeStripe);

            // Configure Stripe webhook secret so the controller uses ConstructWebhookEvent
            services.Configure<ClaudeNest.Backend.Stripe.StripeOptions>(opts =>
            {
                opts.WebhookSecret = "whsec_test_secret";
            });

            // Replace auth
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    public HttpClient CreateAuthenticatedClient(TestUser user)
    {
        var client = CreateClient();
        if (user.IsAuthenticated)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, user.Auth0UserId);
            if (user.Email is not null)
                client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, user.Email);
            if (user.Name is not null)
                client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, user.Name);
        }
        return client;
    }

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        // Apply migrations and seed plans
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
        await db.Database.MigrateAsync();

        // Seed plans
        if (!await db.Plans.AnyAsync())
        {
            db.Plans.AddRange(
                new Plan { Id = WrenPlanId, Name = "Wren", MaxAgents = 1, MaxSessions = 2, PriceCents = 100, IsActive = true, SortOrder = 1 },
                new Plan { Id = RobinPlanId, Name = "Robin", MaxAgents = 2, MaxSessions = 5, PriceCents = 200, IsActive = true, SortOrder = 2 },
                new Plan { Id = HawkPlanId, Name = "Hawk", MaxAgents = 3, MaxSessions = 10, PriceCents = 500, IsActive = true, SortOrder = 3 },
                new Plan { Id = EaglePlanId, Name = "Eagle", MaxAgents = 5, MaxSessions = 25, PriceCents = 1000, IsActive = true, SortOrder = 4 },
                new Plan { Id = FalconPlanId, Name = "Falcon", MaxAgents = 10, MaxSessions = 50, PriceCents = 2000, IsActive = true, SortOrder = 5 },
                new Plan { Id = CondorPlanId, Name = "Condor", MaxAgents = 25, MaxSessions = 100, PriceCents = 5000, IsActive = true, SortOrder = 6 }
            );
            await db.SaveChangesAsync();
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _msSqlContainer.DisposeAsync();
    }

    private static readonly object ScriptCopyLock = new();
    private static bool _scriptsCopied;

    private static void EnsureScriptsAvailable()
    {
        if (_scriptsCopied) return;

        lock (ScriptCopyLock)
        {
            if (_scriptsCopied) return;

            var dir = AppContext.BaseDirectory;
            string? repoRoot = null;
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir, "ClaudeNest.slnx")) ||
                    File.Exists(Path.Combine(dir, "CLAUDE.md")))
                {
                    repoRoot = dir;
                    break;
                }
                dir = Directory.GetParent(dir)?.FullName;
            }

            if (repoRoot is null) return;

            var backendSrcDir = Path.Combine(repoRoot, "src", "ClaudeNest.Backend");
            if (!Directory.Exists(backendSrcDir)) return;

            var targetScriptsDir = Path.Combine(backendSrcDir, "scripts");
            Directory.CreateDirectory(targetScriptsDir);

            var sourceScriptsDir = Path.Combine(repoRoot, "scripts");
            foreach (var file in new[] { "install.sh", "install.ps1" })
            {
                var src = Path.Combine(sourceScriptsDir, file);
                var dst = Path.Combine(targetScriptsDir, file);
                if (File.Exists(src))
                    File.Copy(src, dst, overwrite: true);
            }

            _scriptsCopied = true;
        }
    }
}
