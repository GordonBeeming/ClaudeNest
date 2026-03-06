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
  claudenest-web/            # React frontend (Vite + TypeScript)
```

## Running Locally

```bash
# Start everything via Aspire (requires Docker for SQL Server)
aspire run
```

The Aspire dashboard will be available and orchestrates:
- SQL Server container (for the `nestdb` database)
- ClaudeNest.Backend (API + SignalR hub)
- ClaudeNest.Agent (connects to backend)

See `docs/local-development.md` for detailed setup (ProdLike profile, Auth0, Stripe, database access).

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
| Web frontend | React 19, Vite, TypeScript, Tailwind CSS 4, date-fns, lucide-react, clsx |
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

Tables: `Users`, `Agents`, `AgentCredentials`, `PairingTokens`, `Sessions`, `Plans`, `Accounts`, `AccountLedger`, `Coupons`, `CouponRedemptions`, `CompanyDeals`, `UserFolderPreferences`, `DataProtectionKeys` (see `NestDbContext.cs` for schema)

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

---

## Coding Standards

**IMPORTANT**: These standards MUST be followed consistently. When the user says "do it this way instead", document the new standard here and follow it going forward.

### Backend (C# / ASP.NET Core)

#### EF Core Query Tracking

The DbContext is configured with `QueryTrackingBehavior.NoTracking` as the **default** (set in `Program.cs`). This means:

- **Read-only queries** (returning data to the client): Do NOT add `.AsNoTracking()` — it's already the default
- **Write queries** (updating/deleting entities): MUST explicitly add `.AsTracking()` before the query so EF Core tracks changes for `SaveChangesAsync()`
- Always use `.AsTracking()` when you intend to modify and save an entity
- **NEVER use `FindAsync`** — it does not respect `.AsTracking()` and returns untracked entities when `NoTracking` is the default. Mutations on untracked entities are silently ignored by `SaveChangesAsync()`. Always use `FirstOrDefaultAsync(e => e.Id == id)` instead (with `.AsTracking()` for writes).

```csharp
// READ — no tracking needed (it's the default)
var agent = await db.Agents.FirstOrDefaultAsync(a => a.Id == id);

// WRITE — must opt-in to tracking
var agent = await db.Agents.AsTracking().FirstOrDefaultAsync(a => a.Id == id);
agent.Name = "New name";
await db.SaveChangesAsync();

// BAD — FindAsync returns untracked entity, changes won't persist!
var agent = await db.Agents.FindAsync(id);
agent.Name = "New name";
await db.SaveChangesAsync(); // silently does nothing
```

#### Delete Behavior

- **No cascade delete** — ALL foreign keys use `DeleteBehavior.NoAction`
- This is enforced globally in `NestDbContext.OnModelCreating` via a foreach loop
- All deletes must be explicit — manually remove related entities before removing the parent

#### Entity Conventions

- Entities live in `Data/Entities/` with one class per file
- Entity configurations live in `Data/EntityConfigurations/` — one `IEntityTypeConfiguration<T>` per entity, applied via `ApplyConfigurationsFromAssembly`
- Configurations define: `HasKey`, `HasDefaultValueSql("NEWID()")` for Guid PKs, `HasMaxLength` for strings, `HasDefaultValueSql("SYSUTCDATETIME()")` for timestamps, and relationships via `HasOne`/`HasMany`
- Enums stored as strings via `.HasConversion<string>()`
- Use `Guid` for all primary keys (`Id` property)
- Use `DateTimeOffset` for all date/time properties (not `DateTime`), with default `= DateTimeOffset.UtcNow`
- Navigation properties use `= null!` for required references and `= []` for collections
- Use `required` keyword for properties that must be set at creation

```csharp
public class MyEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Account Account { get; set; } = null!;
    public ICollection<Child> Children { get; set; } = [];
}
```

#### Time Provider

- Use the injected `TimeProvider` (registered as `TimeProvider.System`) for getting current time in services/controllers: `timeProvider.GetUtcNow()`
- Entity default values (like `CreatedAt`) use `DateTimeOffset.UtcNow` since they're set at construction time
- Avoid `DateTime.UtcNow` or `DateTimeOffset.UtcNow` in controller/service logic — always use the injected `TimeProvider`

#### Controller Conventions

- Use **primary constructor DI** — inject dependencies directly in the class declaration
- Inherit from `ControllerBase` (not `Controller`)
- Route pattern: `[Route("api/[controller]")]` with `[ApiController]` attribute
- Use `[Authorize]` at class level; use `[AdminRequired]` (custom `IAsyncActionFilter`) for admin-only controllers
- Return `IActionResult` from all action methods (not typed results)
- Get current user via `User.FindFirst("sub")?.Value` for the Auth0 user ID
- Ownership checks: filter queries with `.Where(a => a.Account.Users.Any(u => u.Auth0UserId == auth0UserId))`
- Use anonymous objects for API responses (no separate response DTOs) — shape the response inline with `new { ... }`
- Request DTOs use `record` types declared at end of controller file: `public record UpdateNameRequest(string Name);`
- Error responses: `BadRequest("message")` or `BadRequest(new { message = "reason" })` — never throw exceptions for expected failures
- Return `NoContent()` for successful deletes, `Ok(data)` for GETs/POSTs with data

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ThingsController(NestDbContext db, TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetThings()
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        if (auth0UserId is null) return Unauthorized();
        // ...
        return Ok(results);
    }
}
```

#### Shared Project (`ClaudeNest.Shared`)

- Contains only DTOs and enums shared between Backend and Agent
- Enums go in `Enums/` folder
- SignalR message types go in `Messages/` folder
- Message classes use `sealed class` with `required` properties and `init` setters

```csharp
public sealed class MyMessage
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? OptionalField { get; init; }
}
```

#### JSON Serialization

- Backend uses `JsonStringEnumConverter` for both controllers and SignalR
- SignalR uses `JsonNamingPolicy.CamelCase` to match frontend expectations
- Agent uses JSON source generators (`AgentJsonContext`) for AOT compatibility — add all serialized types there

#### Service Registration

- Use `AddScoped<>` for services that need per-request lifetime (like `IStripeService`)
- Use `AddSingleton<>` for stateless services (like `TimeProvider`)
- Register in `Program.cs` directly (no separate DI extension methods)

### Frontend (React + TypeScript)

#### Component Patterns

- **All functional components** — no class components
- **Named exports** for all components (`export function MyComponent`)
- Components use props destructuring: `function MyComponent({ prop1, prop2 }: Props)`
- Props interfaces defined above the component in the same file, or inline for simple cases
- One component per file; small helper components (like `OsIcon`) can be in the same file

#### File Organization

```
src/
  api.ts              # All API calls (centralized apiFetch wrapper)
  types.ts            # All TypeScript interfaces/types (centralized)
  config.ts           # Environment configuration
  main.tsx            # App entry point
  App.tsx             # Routing setup
  appInsights.ts      # Application Insights telemetry
  components/         # Reusable UI components
  pages/              # Route-level page components
    admin/            # Admin-only pages
  contexts/           # React context providers
  hooks/              # Custom React hooks
  utils/              # Utility functions
```

#### State Management

- **React Context** for global state (e.g., `UserContext` for user profile/auth)
- **Local state** (`useState`) for component-specific state
- No Redux or other state libraries
- Context providers wrap routes in `App.tsx`

#### API Calls

- All API calls go through `apiFetch<T>()` in `api.ts` — centralized auth token handling and error management
- API functions are named exports (e.g., `getAgents()`, `selectPlan()`)
- Automatic 401 retry with token refresh
- Base URL is `/api` (proxied by Vite in dev)
- Types for API responses are defined in `types.ts`

#### Date Formatting

- Use `date-fns` for all date formatting and manipulation
- **Dates without time**: Use `format(date, "dd MMM yyyy")` → e.g., "06 Mar 2026"
- **Relative time**: Use `formatDistanceToNow(date, { addSuffix: true })` → e.g., "5 minutes ago"
- **Dates with time** (when applicable): Use `format(date, "dd MMM yyyy HH:mm")` → e.g., "06 Mar 2026 14:30"
- Always wrap date strings with `new Date()` before passing to date-fns

#### Styling

- **Tailwind CSS 4** — all styling via utility classes
- **`clsx`** for conditional class merging (not `classnames`)
- Custom color: `nest-*` (project brand color, configured in Tailwind)
- Dark mode: use `dark:` variant classes
- Standard patterns:
  - Cards: `rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900`
  - Buttons: sized with padding, rounded, hover/focus states
  - Text: `text-gray-900 dark:text-white` for primary, `text-gray-500 dark:text-gray-400` for secondary

#### Icons

- Use **lucide-react** for all icons (e.g., `<RefreshCw />`, `<ChevronRight />`)
- Standard sizes: `h-5 w-5` for inline, `h-6 w-6` for larger UI elements, `h-3 w-3` for small badges

#### Routing & Auth

- **react-router-dom v7** for routing
- `RequireAuth` wrapper for authenticated routes (redirects to `/`)
- `RequirePlan` wrapper for routes requiring an active plan (redirects to `/plans`)
- `RequireAdmin` wrapper for admin routes (redirects to `/dashboard`)
- Auth0 integration via `@auth0/auth0-react` with `Auth0Provider` in `App.tsx`

#### SignalR Client

- Custom `useSignalR` hook in `hooks/useSignalR.ts`
- Connection built with `HubConnectionBuilder` pointing to `/hubs/nest`
- Automatic reconnect with backoff: `[0, 1000, 5000, 10000, 30000]`
- Event handlers use callback pattern: `onEventName(handler)` returns cleanup function

#### TypeScript Conventions

- All types/interfaces centralized in `types.ts`
- Use `interface` for object shapes, `type` for unions/aliases
- API response types match backend anonymous object shapes
- String literal unions for enums (e.g., `SessionState`, `SubscriptionStatus`)

#### Error Display

- Errors stored in local state: `const [error, setError] = useState<string | null>(null)`
- Displayed as banners (not toasts): `<div className="rounded-lg bg-red-50 p-3 text-sm text-red-600 dark:bg-red-950/50 dark:text-red-400">{error}</div>`
- Catch API errors with: `catch (e) { setError(e instanceof Error ? e.message : "Failed to load"); }`
- Use browser `confirm()` for destructive actions (delete, cancel subscription, etc.)

#### Code Reuse

- **Extract shared UI into `components/`** — do NOT duplicate UI patterns across pages
- Shared components exist for: `StatusBadge`, `OnlineBadge`, `Select`, `PlanPicker`, `PricingCards`, `Footer`, `Layout`, `AgentCard`, `SessionPanel`, `PastDueBanner`, `AdminUserTable`, `InstallAgentModal`
- If you find yourself writing similar UI in multiple places, extract it into a shared component
- Utility functions go in `utils/` (e.g., `planIntent.ts`)
- Helper functions shared across components go in `types.ts` if type-related (e.g., `formatDiscountDescription`)

### Agent (.NET Worker Service / Native AOT)

#### AOT Compatibility

- `PublishAot=true` — all serialized types MUST be registered in `AgentJsonContext.cs`
- When adding new message types or config types, add `[JsonSerializable(typeof(NewType))]` to `AgentJsonContext`
- Include primitive types used by SignalR (`Guid`, `string`, `bool`, `int`)

#### Project Structure

```
Config/          # Configuration loading (NestConfig, AgentCredentials, ConfigLoader)
Auth/            # HMAC authentication handler
Services/        # Core services (SignalRConnectionManager, SessionManager, DirectoryBrowser)
Serialization/   # JSON source generator context
ServiceInstall/  # Cross-platform service installers (Windows, macOS, Linux)
Worker.cs        # Main hosted service
Program.cs       # Entry point and DI setup
```

#### Key Patterns

- Worker service extends `BackgroundService` with main loop in `ExecuteAsync`
- SignalR connection managed by `SignalRConnectionManager` with automatic reconnection
- Session management via `SessionManager` — spawns `claude remote-control` processes
- Config loaded from `~/.claudenest/config.json` and `~/.claudenest/credentials.json`
- Credentials stored with DPAPI encryption via `CredentialProtector`

---

## Testing

- **All new backend API code must have integration tests** that capture the behavior of the endpoints
- Tests live in `tests/ClaudeNest.Backend.IntegrationTests/Controllers/` — one test class per controller, matching the pattern `{ControllerName}Tests.cs`
- Test infrastructure is in `tests/ClaudeNest.Backend.IntegrationTests/Infrastructure/`:
  - `ClaudeNestWebApplicationFactory` — `WebApplicationFactory` with Testcontainers SQL Server, fake Stripe, and test auth
  - `TestDatabaseHelper` — seed methods for all entities (`SeedUserAsync`, `SeedCouponAsync`, `SeedAgentAsync`, etc.)
  - `TestUsers` — predefined test user identities
  - `FakeStripeService` — records calls for assertion
  - `TestAuthHandler` — header-based auth for tests
- Each test class uses `IClassFixture<ClaudeNestWebApplicationFactory>` with primary constructor injection
- Each test uses a **unique `TestUser`** (unique Auth0 ID) to avoid cross-test interference — do not reuse `TestUsers.UserA` etc. across tests in the same class
- Test naming convention: `MethodName_ExpectedBehavior` or `MethodName_ExpectedBehavior_WhenCondition`
- Use `TestDatabaseHelper.Seed*Async()` to set up test data — add new seed methods when new entity types need seeding
- Assert HTTP status codes and response body properties via `JsonElement`
- **Always verify database state after write operations** — do NOT only assert on the HTTP response body. The response may reflect in-memory state that was never persisted (e.g., due to missing `.AsTracking()`). After any POST/PUT/DELETE that mutates data, create a new scope, get a fresh `NestDbContext`, and assert the entity was actually changed in the database:
  ```csharp
  using var scope = factory.Services.CreateScope();
  var db = scope.ServiceProvider.GetRequiredService<NestDbContext>();
  var dbEntity = await db.Entities.FirstOrDefaultAsync(e => e.Id == id);
  Assert.NotNull(dbEntity);
  Assert.Equal(expectedValue, dbEntity.SomeProperty);
  ```
- Clean up shared state (like plan defaults) at the end of tests that modify shared seed data
- Run tests with: `dotnet test` from the repo root

## Development Guidelines

- **Living documentation** — When adding new features, infrastructure, conventions, or changing existing patterns, update the relevant documentation (`docs/`, `CLAUDE.md`, `README.md`) as part of the same change. Documentation should stay in sync with the codebase, not be an afterthought.
- Backend and Agent both reference `ClaudeNest.Shared` for DTOs — keep message types here
- Agent uses `PublishAot=true` — add all serialized types to `AgentJsonContext.cs`
- Agent config and credentials both use JSON (AOT-compatible via source generators)
- Backend uses EF Core with SQL Server — run Aspire AppHost for local SQL container
- Auth0 config is in `appsettings.json` under `Auth0:Authority` and `Auth0:Audience`
- CORS origins configured in `appsettings.json` under `Cors:Origins`
- The cloud backend NEVER accesses Claude API keys, source code, or local file contents — it is purely a command relay
