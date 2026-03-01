using ClaudeNest.Agent;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();
host.Run();
