# Local Development Guide

This guide covers how to run ClaudeNest locally, configure external services, and work with the database.

## Prerequisites

- .NET 10 SDK
- Docker (for SQL Server via Aspire)
- Node.js + npm (for the React frontend)

---

## Development Profiles

ClaudeNest supports two local profiles, controlled by the environment passed to the Aspire AppHost.

### Development (default)

```bash
dotnet run --project src/ClaudeNest.AppHost
```

This is the fastest way to get running. It requires no external accounts or API keys.

- **Auth0 is bypassed** -- a `DevAuthHandler` auto-authenticates every request as a dev user. No login page, no tokens.
- **Stripe is not required** -- plan selection works locally without Stripe keys. You can pick plans in the UI without processing real payments.
- **SQL Server runs in Docker** via Aspire, on port `1533`.
- **Dev data is seeded automatically** -- plans, a dev user (with `IsAdmin=true`), and a sample agent are created on startup.
- **A dev workspace** is created at `.dev-workspace/` in the repo root with sample folders (`Project-A`, `Project-B`, `Project-C`) for the agent to browse.
- **Web frontend**: `https://localhost:5173`
- **Backend API**: `http://localhost:5180`

### ProdLike

```bash
dotnet run --project src/ClaudeNest.AppHost -- --environment ProdLike
```

This profile enables the real auth and payment stack for integration testing.

- **Auth0 is enabled** -- requires real Auth0 configuration (see below).
- **Stripe is enabled** -- requires real Stripe test keys (see below).
- **EF Core migrations are applied** on startup (the dev seeder does not run).
- **No agent is launched** -- you must run the agent separately or pair one manually.

Use this profile when you need to test the full authentication and payment flows end-to-end.

---

## Auth0 Configuration (ProdLike only)

Auth0 is only needed when running the ProdLike profile. In Development mode, auth is fully bypassed.

### 1. Create an Auth0 tenant and SPA application

- Go to [Auth0](https://auth0.com) and create a tenant.
- Create a new **Single Page Application**.
- Under **Allowed Callback URLs**, **Allowed Logout URLs**, and **Allowed Web Origins**, add `https://localhost:5173`.

### 2. Set user secrets on the AppHost project

```bash
dotnet user-secrets set "Auth0:Domain" "your-tenant.auth0.com" --project src/ClaudeNest.AppHost
dotnet user-secrets set "Auth0:ClientId" "your-spa-client-id" --project src/ClaudeNest.AppHost
dotnet user-secrets set "Auth0:Authority" "https://your-tenant.auth0.com/" --project src/ClaudeNest.AppHost
dotnet user-secrets set "Auth0:Audience" "https://api.claudenest.com" --project src/ClaudeNest.AppHost
```

The AppHost passes these values as environment variables to both the backend (for JWT validation) and the web frontend (for the Auth0 SPA SDK).

### 3. Alternative: `.env.local` for the web frontend

If you are running the frontend outside of Aspire, create `src/claudenest-web/.env.local`:

```
VITE_AUTH0_DOMAIN=your-tenant.auth0.com
VITE_AUTH0_CLIENT_ID=your-spa-client-id
VITE_AUTH0_AUDIENCE=https://api.claudenest.com
```

When running via Aspire, these values are injected automatically from user secrets -- you do not need the `.env.local` file.

---

## Stripe Configuration

Stripe is optional in Development mode (plan selection works without it) but required for ProdLike.

### 1. Get test keys

- Create a [Stripe](https://stripe.com) account or use an existing one.
- Make sure you are in **test mode** (toggle in the Stripe dashboard).
- Copy your test-mode **Secret Key** (`sk_test_...`) and **Publishable Key** (`pk_test_...`).

### 2. Set user secrets on the Backend project

```bash
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..." --project src/ClaudeNest.Backend
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..." --project src/ClaudeNest.Backend
```

On startup, the backend auto-syncs plans to Stripe (creates products and prices as needed).

---

## Stripe CLI for Webhooks

Stripe sends events (e.g., checkout completed, subscription updated) via webhooks. During local development, the Stripe CLI forwards these to your local backend.

### 1. Install the Stripe CLI

```bash
brew install stripe/stripe-cli/stripe
```

If Homebrew has issues, download the binary directly from [Stripe CLI GitHub releases](https://github.com/stripe/stripe-cli/releases) and place it in `~/bin/stripe` (or anywhere on your `PATH`).

### 2. Login

```bash
stripe login
```

### 3. Forward webhooks to your local backend

```bash
stripe listen --forward-to http://localhost:5180/api/stripe/webhook
```

This prints a webhook signing secret (`whsec_...`) to the terminal.

### 4. (Optional) Configure the webhook secret

```bash
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..." --project src/ClaudeNest.Backend
```

If no webhook secret is configured, signature verification is skipped in dev mode (a warning is logged).

### 5. Webhook trace files

When `Stripe:TraceWebhooks` is `true` (enabled by default in `appsettings.json`), raw webhook JSON payloads are written to `.webhook-traces/` at the repo root. This directory is gitignored. Useful for debugging webhook handling.

---

## Stripe Test Cards

Use these card numbers in the Stripe checkout UI during testing. Any future expiry date, any 3-digit CVC, and any billing postal code will work.

| Card Number              | Behavior                           |
|--------------------------|------------------------------------|
| `4242 4242 4242 4242`    | Successful payment                 |
| `4000 0000 0000 3220`    | 3D Secure authentication required  |
| `4000 0000 0000 0002`    | Card declined                      |

---

## Database

### SQL Server via Aspire

SQL Server runs in Docker, managed by Aspire. It starts automatically with `dotnet run --project src/ClaudeNest.AppHost`.

- **Port**: `1533`
- **Connection string**: `Server=127.0.0.1,1533;User ID=sa;Password=DevPass123!;TrustServerCertificate=true;Initial Catalog=nestdb`
- **Data volume**: `nestdb-data` (data persists across container restarts)

### Querying the database

Use `docker exec` with `sqlcmd` inside the running container, or install a local SQL client such as `mssql-cli` or `sqlcmd`.

### EF Core migrations

Create a new migration:

```bash
dotnet ef migrations add <Name> --project src/ClaudeNest.Backend
```

Apply pending migrations:

```bash
dotnet ef database update --project src/ClaudeNest.Backend
```

In Development mode, migrations are applied automatically on startup alongside dev data seeding. In ProdLike mode, only migrations are applied (no seed data).
