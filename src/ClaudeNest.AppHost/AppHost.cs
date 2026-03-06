using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// User secrets are only loaded automatically in Development.
// Load them explicitly for ProdLike so Auth0 config is available.
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsProduction())
{
    builder.Configuration.AddUserSecrets(Assembly.GetEntryAssembly()!, optional: true);
}

var isDev = builder.Environment.IsDevelopment();

var sqlPassword = builder.AddParameter("sql-password", secret: true, value: "DevPass123!");

var sql = builder.AddSqlServer("sql", sqlPassword, port: 1533)
    .WithDataVolume("nestdb-data")
    .AddDatabase("nestdb");

var backend = builder.AddProject<Projects.ClaudeNest_Backend>("backend")
    .WithReference(sql)
    .WaitFor(sql)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithHttpEndpoint(port: 5180, name: "api");

if (isDev)
{
    // Dev workspace: create sample project folders for browsing
    var devWorkspace = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", ".dev-workspace"));
    Directory.CreateDirectory(Path.Combine(devWorkspace, "Project-A"));
    Directory.CreateDirectory(Path.Combine(devWorkspace, "Project-B"));
    Directory.CreateDirectory(Path.Combine(devWorkspace, "Project-C"));

    backend.WithEnvironment("DevWorkspacePath", devWorkspace);

    builder.AddProject<Projects.ClaudeNest_Agent>("agent")
        .WithReference(backend)
        .WaitFor(backend)
        .WithArgs("run")
        .WithEnvironment("Agent__BackendUrl", backend.GetEndpoint("api"))
        .WithEnvironment("Agent__AgentId", "00000000-0000-0000-0000-000000000002")
        .WithEnvironment("Agent__Secret", "dev-secret-do-not-use-in-production")
        .WithEnvironment("Agent__AllowedPaths__0", devWorkspace)
        .WithEnvironment("Agent__Name", "Local Dev Agent");
}

var web = builder.AddViteApp("web", "../claudenest-web", "dev")
    .WithNpm()
    .WithReference(backend)
    .WaitFor(backend)
    .WithEnvironment("BACKEND_URL", backend.GetEndpoint("api"))
    .WithEnvironment("PORT", "5173")
    .WithHttpsEndpoint(port: 5173, isProxied: false);

// Pass Auth0 config when not in Development (Auth0 env vars come from user secrets / configuration)
if (!isDev)
{
    var auth0Domain = builder.Configuration["Auth0:Domain"];
    var auth0ClientId = builder.Configuration["Auth0:ClientId"];
    var auth0Authority = builder.Configuration["Auth0:Authority"];
    var auth0Audience = builder.Configuration["Auth0:Audience"];

    // Frontend needs domain + clientId + audience for the Auth0 SPA SDK
    if (!string.IsNullOrEmpty(auth0Domain))
        web.WithEnvironment("VITE_AUTH0_DOMAIN", auth0Domain);
    if (!string.IsNullOrEmpty(auth0ClientId))
        web.WithEnvironment("VITE_AUTH0_CLIENT_ID", auth0ClientId);
    if (!string.IsNullOrEmpty(auth0Audience))
        web.WithEnvironment("VITE_AUTH0_AUDIENCE", auth0Audience);

    // Backend needs authority + audience for JWT validation (overrides appsettings placeholders)
    if (!string.IsNullOrEmpty(auth0Authority))
        backend.WithEnvironment("Auth0__Authority", auth0Authority);
    if (!string.IsNullOrEmpty(auth0Audience))
        backend.WithEnvironment("Auth0__Audience", auth0Audience);
}

builder.Build().Run();
