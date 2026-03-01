using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeNest.Backend.Auth;
using ClaudeNest.Backend.Data;
using ClaudeNest.Backend.Hubs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Database
builder.AddSqlServerDbContext<NestDbContext>("nestdb");

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
        });
}
builder.Services.AddAuthorization();

// SignalR — use camelCase + string enums to match frontend expectations
builder.Services.AddSignalR(options =>
    {
        if (builder.Environment.IsDevelopment())
            options.EnableDetailedErrors = true;
    })
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Controllers
builder.Services.AddControllers();

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
}
else
{
    // ProdLike / Production: apply EF migrations
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
    await db.Database.MigrateAsync();
}

app.MapDefaultEndpoints();

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<AgentAuthMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NestHub>("/hubs/nest");

app.Run();
