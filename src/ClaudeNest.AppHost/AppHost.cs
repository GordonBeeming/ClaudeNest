var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .AddDatabase("nestdb");

var backend = builder.AddProject<Projects.ClaudeNest_Backend>("backend")
    .WithReference(sql)
    .WaitFor(sql);

builder.AddProject<Projects.ClaudeNest_Agent>("agent")
    .WithReference(backend)
    .WaitFor(backend)
    .WithEnvironment("Agent__BackendUrl", backend.GetEndpoint("http"))
    .WithEnvironment("Agent__AgentId", "00000000-0000-0000-0000-000000000002")
    .WithEnvironment("Agent__Secret", "dev-secret-do-not-use-in-production");

builder.Build().Run();
