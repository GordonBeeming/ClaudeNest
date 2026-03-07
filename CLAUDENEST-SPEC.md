# ClaudeNest — Project Specification

> A lightweight service that lets you browse your dev folders remotely and launch Claude Code remote-control sessions from anywhere.

## Overview

ClaudeNest is a three-component system:

1. **Web Dashboard** — Login, see connected agents/machines, browse allowed folders, launch/stop Claude remote-control sessions
2. **Cloud Backend** — Stateless ASP.NET Core API + SignalR hub that relays commands between the web and agents
3. **Local Agent** — .NET 9 AOT Worker Service that runs as a system service, connects outbound to the backend, serves folder listings, and spawns `claude remote-control` processes

The key insight: ClaudeNest does NOT stream any terminal I/O. It simply spawns `claude remote-control` (Anthropic's native feature, shipped Feb 25 2026) with a specified working directory. The user then opens `claude.ai/code` or the Claude mobile app to interact with the session. ClaudeNest is purely a remote launcher and session manager.

---

## Architecture

```
┌────────────────────────────────┐
│     ClaudeNest Web App         │
│  (React + Auth0)               │
│                                │
│  • Auth0 login (Google/GitHub) │
│  • See connected agents        │
│  • Browse allowed folders      │
│  • "Launch session here"       │  → user then goes to claude.ai/code
│  • See active sessions         │
│  • "Stop session"              │
└──────────┬─────────────────────┘
           │ SignalR (JS client)
┌──────────▼─────────────────────┐
│     ASP.NET Core Backend       │
│                                │
│  • Auth0 JWT validation        │
│  • SignalR Hub (relay)         │
│  • Agent credential validation │
│  • Azure SignalR Service       │
│  • Azure SQL Serverless        │
└──────────┬─────────────────────┘
           │ SignalR (.NET client, outbound only)
┌──────────▼─────────────────────┐
│     ClaudeNest Agent (.NET 9)  │
│  (AOT compiled, single binary) │
│                                │
│  • Connects outbound to hub    │
│  • Serves folder tree          │
│  • Spawns/stops claude         │
│    remote-control processes    │
│  • Reports session status      │
│  • Enforces allow/deny paths   │
│  • Runs as system service      │
└────────────────────────────────┘
```

---

## Tech Stack (decided)

| Component | Technology | Why |
|---|---|---|
| Agent | .NET 9 Worker Service, Native AOT, SignalR client | Single binary (~12MB), cross-platform service install, first-class SignalR support |
| Backend | ASP.NET Core 9 + SignalR Hub | Same ecosystem as agent, minimal code |
| Real-time relay | Azure SignalR Service (managed) | Free tier for dev (20 connections), Standard for prod ($49/mo per 1K connections). Same code as in-process SignalR — one config line to switch |
| Database | Azure SQL Serverless | Pauses when idle ($0). 6 tables. Scales to Hyperscale. Already familiar tech |
| Auth (users) | Auth0 | 25K MAU free. Social login (Google, GitHub) via toggle. JWT validation in ASP.NET Core |
| Auth (agents) | Custom — simple hashed secret in DB | NOT Auth0 M2M. Agent stores {agentId, secret} locally; backend validates against hash |
| Web frontend | React (or Next.js) + @auth0/auth0-react + @microsoft/signalr | Standard SPA, static hosting |
| Hosting | Azure Container Apps (backend), Azure Static Web Apps (frontend) | Cheap, scales to zero |

### Scaling path (no tech changes needed)
```
Dev:          In-process SignalR (no Azure dependency)
Launch:       Azure SignalR Free tier (20 connections, $0)
100 users:    Azure SignalR Standard, 1 unit ($49/mo)
1,000 users:  Standard, 3 units ($147/mo)  
100K users:   Premium, autoscale ($3-5K/mo)
```

---

## Database Schema (Azure SQL)

```sql
CREATE TABLE Users (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Auth0UserId     NVARCHAR(128) NOT NULL UNIQUE,  -- from Auth0 JWT sub claim
    Email           NVARCHAR(256) NOT NULL,
    DisplayName     NVARCHAR(256),
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE Agents (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId          UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id),
    Name            NVARCHAR(256),          -- user-friendly name
    Hostname        NVARCHAR(256),          -- machine hostname
    OS              NVARCHAR(64),           -- win/mac/linux
    IsOnline        BIT NOT NULL DEFAULT 0,
    LastSeenAt      DATETIME2,
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE AgentCredentials (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    AgentId         UNIQUEIDENTIFIER NOT NULL REFERENCES Agents(Id),
    SecretHash      VARBINARY(64) NOT NULL,     -- SHA-256 of the secret
    IssuedAt        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    RevokedAt       DATETIME2 NULL,
    LastUsedAt      DATETIME2 NULL
);

CREATE TABLE PairingTokens (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId          UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id),
    TokenHash       VARBINARY(64) NOT NULL,     -- SHA-256 of the token
    ExpiresAt       DATETIME2 NOT NULL,         -- short-lived, e.g. 10 min
    RedeemedAt      DATETIME2 NULL
);

CREATE TABLE Sessions (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    AgentId         UNIQUEIDENTIFIER NOT NULL REFERENCES Agents(Id),
    Path            NVARCHAR(1024) NOT NULL,    -- working directory
    State           NVARCHAR(32) NOT NULL,      -- Starting/Running/Stopping/Stopped/Crashed
    Pid             INT NULL,                   -- OS process ID (while running)
    StartedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    EndedAt         DATETIME2 NULL,
    ExitCode        INT NULL
);
```

That's it. Six tables.

---

## Authentication Design

### Two separate auth layers:

**Layer 1: Auth0 → User identity (web dashboard)**
- User logs in via Auth0 (Google, GitHub, or email/password)
- Auth0 returns JWT
- Backend validates JWT:
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://YOUR_TENANT.auth0.com/";
        options.Audience = "https://api.claudenest.app";
    });
```

**Layer 2: Custom → Agent credentials (agent ↔ backend)**
- Agent authenticates with agentId + secret in headers
- Backend validates against SecretHash in AgentCredentials table
- Auth0 is NOT involved in agent auth at all

### Pairing flow:
1. User logs into web dashboard (Auth0)
2. User clicks "Add Agent" → backend generates short-lived pairing token (10 min, one-use)
3. User runs: `claudenest install --token <PAIRING_TOKEN>`
4. Agent sends pairing token to backend
5. Backend validates token → creates Agent + AgentCredential in DB
6. Returns {agentId, secret} to agent (plaintext, one time only)
7. Agent stores in `~/.claudenest/credentials.json` (owner-only permissions)
8. Pairing token is burned

### Agent connection:
```
Agent → SignalR hub with headers:
  X-Agent-Id: <agentId>
  X-Agent-Secret: <secret>

Hub middleware validates against SecretHash in DB
```

---

## SignalR Hub Contract

```
Hub endpoint: /hubs/nest

── Agent → Server ──
RegisterAgent(agentInfo)                    // on connect, sends hostname/OS/etc
SessionStatusChanged(sessionUpdate)         // on every session state change
DirectoryListing(requestId, folders[])      // response to browse request
ReportAllSessions(sessions[])              // on reconnect, sync full state
Heartbeat()                                 // keepalive

── Server → Agent ──
ListDirectories(requestId, path)
StartSession(sessionId, path)
StopSession(sessionId)
GetSessions()                               // request full state

── Server → Web Client ──
AgentStatusChanged(agentId, status)
DirectoryListingResult(requestId, folders[])
SessionStatusChanged(sessionId, status)
```

No I/O streaming. The heaviest message is a directory listing. Everything else is small control messages.

---

## Agent Configuration

File: `~/.claudenest/config.yaml`

```yaml
# Which directories are browsable from the web dashboard
allowed_paths:
  - /Users/gordon/dev
  - /Users/gordon/projects

# Explicit denies (takes precedence over allowed)
denied_paths:
  - /Users/gordon/dev/secrets-repo

# Claude CLI path (auto-detected or explicit)
claude_binary: claude

# Max concurrent sessions
max_sessions: 3
```

File: `~/.claudenest/credentials.json` (generated during install, owner-only permissions)

```json
{
    "agentId": "a1b2c3d4-...",
    "secret": "base64-encoded-256-bit-key",
    "backendUrl": "https://api.claudenest.app"
}
```

---

## Agent: Session Management

### Session states
```
Requested → Starting → Running → Stopping → Stopped
                                            → Crashed
```

### Core spawning logic
The agent does NOT use PTY or terminal emulation. It simply spawns a process:

```csharp
var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = config.ClaudeBinaryPath ?? "claude",
        Arguments = "remote-control",
        WorkingDirectory = resolvedPath,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    },
    EnableRaisingEvents = true
};
process.Start();
```

The user then goes to `claude.ai/code` to interact with the session. Claude handles all the I/O, authentication, and UI.

### Session tracking requirements
- Track process in ConcurrentDictionary<sessionId, ManagedSession>
- ManagedSession holds: SessionId, Path, Pid, State, StartedAt, EndedAt, ExitCode
- Health check timer every 30 seconds — detect zombie processes via Process.GetProcessById
- On process exit: update state, notify backend via SignalR
- On reconnect: dump full session list to backend to sync state
- Cleanup old stopped/crashed sessions after 1 hour

### Path validation
- Resolve path to full absolute path
- Check against allowed_paths (must be within at least one)
- Check against denied_paths (must not be within any)
- Check max_sessions limit

---

## Agent: Directory Browser

Only returns DIRECTORIES, not files. Only returns entries within allowed_paths and not within denied_paths.

```csharp
public List<string> List(string path)
{
    var resolved = Path.GetFullPath(path);
    
    if (!config.AllowedPaths.Any(a => resolved.StartsWith(a)))
        return [];
    
    if (config.DeniedPaths.Any(d => resolved.StartsWith(d)))
        return [];
    
    return Directory.GetDirectories(resolved)
        .Select(Path.GetFileName)
        .ToList();
}
```

---

## Agent: Installation & Service Registration

```bash
claudenest install --token <PAIRING_TOKEN>
```

This should:
1. Validate the pairing token against the backend
2. Exchange it for a long-lived agent credential
3. Store credential in `~/.claudenest/credentials.json`
4. Create default config in `~/.claudenest/config.yaml`
5. Register as a system service:
   - **Linux**: systemd unit
   - **macOS**: launchd plist
   - **Windows**: Windows Service (sc.exe or installer)
6. Start the service

---

## Agent: AOT Compilation Notes

```xml
<!-- .csproj -->
<PropertyGroup>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>true</InvariantGlobalization>
</PropertyGroup>
```

**Important**: SignalR client works with AOT but requires source generators for System.Text.Json:

```csharp
[JsonSerializable(typeof(DirectoryListingResponse))]
[JsonSerializable(typeof(SessionStatusUpdate))]
[JsonSerializable(typeof(AgentInfo))]
// ... all types sent over SignalR
internal partial class AgentJsonContext : JsonSerializerContext { }
```

Publish per platform:
```bash
dotnet publish -r win-x64 -c Release
dotnet publish -r linux-x64 -c Release  
dotnet publish -r osx-arm64 -c Release
```

---

## Web Dashboard Features (MVP)

### Pages
1. **Login** — Auth0 hosted login page
2. **Dashboard** — List of agents (online/offline, hostname, OS, last seen)
3. **Agent detail** — Folder tree browser + active sessions list
4. **Session controls** — Launch button (on folder), Stop button (on session), status indicators

### UI for folder browsing
- Tree view of allowed directories
- Click to expand (lazy-loaded via SignalR → agent → SignalR → browser)
- "Launch Session Here" button on each folder
- Breadcrumb navigation

### UI for session management
```
Agent: Gordon's MacBook (🟢 online)
────────────────────────────────────
Active Sessions (2 of 3 max)

📁 ~/dev/api-project
   🟢 Running | PID: 48291 | Started: 2 min ago
   [Stop Session]

📁 ~/dev/frontend  
   🟢 Running | PID: 48305 | Started: 45 min ago
   [Stop Session]

Recent Sessions
📁 ~/dev/scripts
   ⚪ Stopped | Exit: 0 | Ran for: 1h 23m
```

---

## Security Model

| Layer | Mechanism |
|---|---|
| Web auth | Auth0 JWT (Google/GitHub/email social login) |
| Agent pairing | Short-lived token (10 min, one-use, SHA-256 hashed in DB) |
| Agent credential | Long-lived {agentId, secret}, secret hashed in DB, stored locally with restricted perms |
| Transport | WSS (TLS) via Azure SignalR Service |
| Folder browsing | Agent enforces allow/deny locally — backend never sees unapproved paths |
| Claude credentials | Stored on local machine only; agent spawns Claude CLI which uses its own auth; NEVER transits the cloud |
| Session isolation | Each Claude process runs as the local user; normal OS permissions apply |

**The cloud backend never has access to Claude API keys, source code, or local file contents. It is purely a command relay.**

---

## Pricing Model

**$1 AUD per agent per month** (~$0.63 USD)

- Free tier: 1 agent (for trying it out)
- Paid: $1 AUD/agent/month

### Economics summary
| Users | Agents | Revenue (USD/mo) | Cost (USD/mo) | Margin |
|---|---|---|---|---|
| 10 | 20 | $12.60 | ~$5 | 60% |
| 100 | 200 | $126 | ~$65 | 48% |
| 1,000 | 2,000 | $1,260 | ~$210 | 83% |
| 10,000 | 20,000 | $12,600 | ~$1,770 | 86% |
| 100,000 | 200,000 | $126,000 | ~$4,850 | 96% |

Breakeven: ~10 users. All infrastructure starts on free tiers.

---

## Project Structure (suggested)

```
claudenest/
├── src/
│   ├── ClaudeNest.Agent/               # .NET 9 Worker Service (AOT)
│   │   ├── Program.cs
│   │   ├── Services/
│   │   │   ├── AgentHostedService.cs    # Main lifecycle
│   │   │   ├── SignalRConnectionManager.cs
│   │   │   ├── DirectoryBrowser.cs
│   │   │   ├── SessionManager.cs
│   │   │   └── SessionProcess.cs
│   │   ├── Config/
│   │   │   ├── NestConfig.cs
│   │   │   └── PathFilter.cs
│   │   ├── Install/
│   │   │   ├── ServiceInstaller.cs      # systemd/launchd/windows service
│   │   │   └── TokenExchange.cs
│   │   ├── Serialization/
│   │   │   └── AgentJsonContext.cs       # AOT source generators
│   │   └── ClaudeNest.Agent.csproj
│   │
│   ├── ClaudeNest.Backend/             # ASP.NET Core API + SignalR Hub
│   │   ├── Program.cs
│   │   ├── Hubs/
│   │   │   └── NestHub.cs
│   │   ├── Controllers/
│   │   │   ├── AgentsController.cs
│   │   │   ├── PairingController.cs
│   │   │   └── SessionsController.cs
│   │   ├── Auth/
│   │   │   └── AgentAuthMiddleware.cs
│   │   ├── Data/
│   │   │   ├── NestDbContext.cs          # EF Core
│   │   │   └── Entities/
│   │   └── ClaudeNest.Backend.csproj
│   │
│   ├── ClaudeNest.Shared/              # Shared DTOs/contracts
│   │   ├── Messages/
│   │   │   ├── DirectoryListingRequest.cs
│   │   │   ├── DirectoryListingResponse.cs
│   │   │   ├── StartSessionRequest.cs
│   │   │   ├── SessionStatusUpdate.cs
│   │   │   └── AgentInfo.cs
│   │   └── ClaudeNest.Shared.csproj
│   │
│   └── claudenest-web/                 # React frontend
│       ├── src/
│       │   ├── components/
│       │   │   ├── AgentList.tsx
│       │   │   ├── FolderTree.tsx
│       │   │   ├── SessionPanel.tsx
│       │   │   └── Layout.tsx
│       │   ├── hooks/
│       │   │   └── useSignalR.ts
│       │   ├── pages/
│       │   │   ├── Dashboard.tsx
│       │   │   ├── AgentDetail.tsx
│       │   │   └── Login.tsx
│       │   └── App.tsx
│       └── package.json
│
├── infra/                               # Infrastructure as code
│   ├── bicep/ or terraform/
│   └── README.md
│
├── CLAUDE.md                            # → copy this spec file here
├── README.md
└── claudenest.sln
```

---

## Prerequisites for Claude Remote Control

The user must have ALREADY run `claude` and `/login` on the agent machine at least once. ClaudeNest does not manage Claude authentication — it only spawns the process. The agent installer should check for the `claude` binary and warn if not found.

Required on agent machine:
- Claude Code CLI v2.1.52+
- Claude Max subscription (Pro support coming soon)
- Authenticated via `/login`

---

## MVP Build Order

1. **Shared project** — Define all SignalR message DTOs
2. **Backend** — EF Core entities + migrations, SignalR hub (hardcode a test agent at first), Auth0 JWT middleware, pairing endpoint
3. **Agent** — Config loading, SignalR connection, directory browser, session manager, process spawning. Test with in-process SignalR first
4. **Pairing flow** — Token generation (backend) → token exchange (agent) → credential storage
5. **Web frontend** — Auth0 login, agent list, folder tree browser, session launch/stop UI
6. **Service installation** — Cross-platform service registration in the agent
7. **Azure deployment** — Switch to Azure SignalR Service, deploy backend to Container Apps, frontend to Static Web Apps
8. **Polish** — Error handling, reconnection logic, health checks, logging

---

## Key Design Decisions (for context)

- **Azure SignalR over Web PubSub**: We're .NET end-to-end. SignalR gives us auto-reconnect, strongly-typed hubs, and RPC-style invoke for free. Same pricing. Same scaling path. Web PubSub would mean building our own message envelope and reconnection logic.
- **Azure SQL over Cosmos DB**: Data is relational (users → agents → sessions). Only 6 tables. SQL Serverless pauses when idle ($0). We know the tech. Cosmos only wins at 50K+ users with global distribution needs.
- **Auth0 over DIY OAuth**: 25K MAU free. Every hour spent on auth is an hour not spent on the product. Auth0 handles social login, password reset, MFA (future) with zero maintenance.
- **Agent credentials over Auth0 M2M**: Auth0 M2M requires creating a new Auth0 Application per agent — doesn't scale. Simple hashed secret in our own DB is cleaner and free.
- **No terminal streaming**: Claude remote-control handles ALL I/O through Anthropic's own infrastructure. ClaudeNest just spawns the process. This removes 80% of the complexity (no PTY, no xterm.js, no bidirectional streaming).
- **.NET AOT for agent**: Single binary per platform. No runtime dependency. Easy service installation. SignalR client is compatible with AOT using JSON source generators.
