# ClaudeNest

A lightweight service that lets you browse dev folders remotely and launch Claude Code remote-control sessions from anywhere.

## Architecture

Three-component system:
1. **Web Dashboard** (React + Auth0) — login, browse agents/folders, launch/stop sessions
2. **Cloud Backend** (ASP.NET Core + SignalR) — stateless API that relays commands between web and agents
3. **Local Agent** (.NET 10 AOT Worker Service) — runs on dev machines, connects outbound to backend, spawns `claude remote-control` processes

**Key insight**: ClaudeNest does NOT stream terminal I/O. It spawns `claude remote-control` (Anthropic's native feature) with a working directory. Users interact via `claude.ai/code`. ClaudeNest is purely a remote launcher/session manager.

## Project Structure

```
src/
  ClaudeNest.AppHost/        # Aspire orchestrator (local dev)
  ClaudeNest.ServiceDefaults/ # Shared Aspire service configuration
  ClaudeNest.Shared/         # Shared DTOs and enums (referenced by Backend + Agent)
  ClaudeNest.Backend/        # ASP.NET Core API + SignalR Hub
  ClaudeNest.Agent/          # .NET 10 AOT Worker Service
```

## Running Locally

```bash
# Start everything via Aspire (requires Docker for SQL Server)
dotnet run --project src/ClaudeNest.AppHost
```

The Aspire dashboard will be available and orchestrates:
- SQL Server container (for the `nestdb` database)
- ClaudeNest.Backend (API + SignalR hub)
- ClaudeNest.Agent (connects to backend)

## Build & Test

```bash
dotnet build           # Build all projects
dotnet test            # Run all tests
```

## Tech Stack

| Component | Technology |
|---|---|
| Agent | .NET 10 Worker Service, Native AOT, SignalR client |
| Backend | ASP.NET Core 10 + SignalR Hub + EF Core |
| Database | SQL Server (Azure SQL Serverless in prod) |
| Auth (users) | Auth0 JWT (Google/GitHub social login) |
| Auth (agents) | Custom hashed secret in DB |
| Web frontend | React + @auth0/auth0-react + @microsoft/signalr |
| Local dev | .NET Aspire for orchestration |

## Key Design Decisions

- **No terminal streaming** — Claude remote-control handles all I/O. This removes ~80% of complexity.
- **Two separate auth layers** — Auth0 for web users, custom hashed secrets for agents. Agents never touch Auth0.
- **Azure SignalR Service in prod** — Same code as in-process SignalR; one config line to switch. Free tier for dev.
- **Agent is AOT compiled** — Single binary per platform, no runtime dependency. Uses JSON source generators.

## SignalR Hub Contract

Hub endpoint: `/hubs/nest`

**Agent -> Server**: `RegisterAgent`, `SessionStatusChanged`, `DirectoryListing`, `ReportAllSessions`, `Heartbeat`

**Server -> Agent**: `ListDirectories`, `StartSession`, `StopSession`, `GetSessions`

**Server -> Web Client**: `AgentStatusChanged`, `DirectoryListingResult`, `SessionStatusChanged`, `AllSessionsUpdated`

## Database

6 tables: `Users`, `Agents`, `AgentCredentials`, `PairingTokens`, `Sessions` (see `NestDbContext.cs` for schema)

EF Core migrations target: `src/ClaudeNest.Backend`

```bash
# Create a migration
dotnet ef migrations add <Name> --project src/ClaudeNest.Backend

# Apply migrations
dotnet ef database update --project src/ClaudeNest.Backend
```

## Agent Configuration

Agent config lives in `~/.claudenest/`:
- `config.json` — allowed/denied paths, claude binary path, max sessions
- `credentials.json` — agentId, secret, backendUrl (generated during pairing)

## Session States

```
Requested -> Starting -> Running -> Stopping -> Stopped
                                             -> Crashed
```

## Development Guidelines

- Backend and Agent both reference `ClaudeNest.Shared` for DTOs — keep message types here
- Agent uses `PublishAot=true` — add all serialized types to `AgentJsonContext.cs`
- Agent config and credentials both use JSON (AOT-compatible via source generators)
- Backend uses EF Core with SQL Server — run Aspire AppHost for local SQL container
- Auth0 config is in `appsettings.json` under `Auth0:Authority` and `Auth0:Audience`
- CORS origins configured in `appsettings.json` under `Cors:Origins`
- The cloud backend NEVER accesses Claude API keys, source code, or local file contents — it is purely a command relay

## MVP Build Order

1. Shared DTOs (done)
2. Backend — EF Core entities + migrations, SignalR hub, Auth0 JWT, pairing endpoint
3. Agent — Config loading, SignalR connection, directory browser, session manager
4. Pairing flow — Token generation + exchange + credential storage
5. Web frontend — Auth0 login, agent list, folder tree, session controls
6. Service installation — Cross-platform service registration
7. Azure deployment — Azure SignalR Service, Container Apps, Static Web Apps
8. Polish — Error handling, reconnection, health checks, logging
