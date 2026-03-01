using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var isDev = builder.Environment.IsDevelopment();

var sqlPassword = builder.AddParameter("sql-password", secret: true, value: "DevPass123!");

var sql = builder.AddSqlServer("sql", sqlPassword)
    .WithDataVolume("nestdb-data")
    .AddDatabase("nestdb");

var backend = builder.AddProject<Projects.ClaudeNest_Backend>("backend")
    .WithReference(sql)
    .WaitFor(sql);

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
        .WithEnvironment("Agent__BackendUrl", backend.GetEndpoint("http"))
        .WithEnvironment("Agent__AgentId", "00000000-0000-0000-0000-000000000002")
        .WithEnvironment("Agent__Secret", "dev-secret-do-not-use-in-production")
        .WithEnvironment("Agent__AllowedPaths__0", devWorkspace)
        .WithEnvironment("Agent__Name", "Local Dev Agent");
}

var web = builder.AddViteApp("web", "../claudenest-web")
    .WithNpm()
    .WithReference(backend)
    .WaitFor(backend)
    .WithEnvironment("BACKEND_URL", backend.GetEndpoint("http"));

// Pass Auth0 config to frontend when not in Development (Auth0 env vars come from user secrets / configuration)
if (!isDev)
{
    var auth0Domain = builder.Configuration["Auth0:Domain"];
    var auth0ClientId = builder.Configuration["Auth0:ClientId"];
    var auth0Audience = builder.Configuration["Auth0:Audience"];

    if (!string.IsNullOrEmpty(auth0Domain))
        web.WithEnvironment("VITE_AUTH0_DOMAIN", auth0Domain);
    if (!string.IsNullOrEmpty(auth0ClientId))
        web.WithEnvironment("VITE_AUTH0_CLIENT_ID", auth0ClientId);
    if (!string.IsNullOrEmpty(auth0Audience))
        web.WithEnvironment("VITE_AUTH0_AUDIENCE", auth0Audience);
}

builder.Build().Run();
