# ClaudeNest Architecture

## System Overview

ClaudeNest is a three-component system that lets you browse dev folders remotely and launch Claude Code remote-control sessions from anywhere.

```
                         Internet
                            |
                    +-------+-------+
                    |  Cloudflare   |
                    |   (DNS/CDN)   |
                    +-------+-------+
                            |
                            | HTTPS
                            v
+-----------------------------------------------------------+
|              Azure Resource Group                         |
|                                                           |
|  +-------------------+    Cloudflare Tunnel (outbound)    |
|  |   cloudflared     |<=================================> |
|  | (Container App)   |                                    |
|  +--------+----------+                                    |
|           |                                               |
|    VNet (internal)                                        |
|     |            |                                        |
|     v            v                                        |
|  +------+   +--------+                                   |
|  | Web  |   |  API   |                                   |
|  | (CA) |   |  (CA)  |---+                               |
|  +------+   +---+----+   |                               |
|                 |         |                               |
|          +------+----+   +--------+                      |
|          |  SignalR   |  | SQL DB |                      |
|          |  Service   |  +--------+                      |
|          +-----------+                                   |
+-----------------------------------------------------------+
       ^
       | SignalR (outbound WebSocket)
       |
+------+------+
| Local Agent |  (dev machine)
+------+------+
       |
       | spawns
       v
  claude remote-control
```

## Traffic Flow

### Web Users

1. User navigates to `https://claudenest.app`
2. Cloudflare DNS resolves and routes to the Cloudflare Tunnel
3. `cloudflared` container (running in Azure) receives the request over the tunnel
4. Routing rules forward the request to the appropriate internal container app:
   - `/api/*` -> Backend Container App (API + SignalR Hub)
   - Everything else -> Frontend Container App (React SPA via nginx)
5. All container apps are on an **internal VNet** -- not directly exposed to the internet

### Agent Connections

1. The local agent initiates an **outbound** WebSocket connection to `https://claudenest.app/api/hubs/nest`
2. Traffic flows through Cloudflare -> cloudflared tunnel -> Backend Container App -> Azure SignalR Service
3. The agent authenticates with a hashed secret (not Auth0)
4. Commands (start session, list directories) are relayed via SignalR
5. The agent spawns `claude remote-control` processes locally -- ClaudeNest never streams terminal I/O

## Cloudflare Tunnel

All inbound traffic to ClaudeNest flows through a **Cloudflare Tunnel** -- there are no public IPs or Azure ingress controllers. The `cloudflared` container (running as an Azure Container App) establishes an outbound-only connection to Cloudflare, and route rules in the Cloudflare dashboard direct traffic to the appropriate internal services.

### Tunnel Configuration

- **Tunnel name**: `ca-claudenest-tunnel-{env}` (e.g. `ca-claudenest-tunnel-prod`)
- **Tunnel token**: Stored in Azure Key Vault as `cloudflare-tunnel-token`, injected into the `cloudflared` container at runtime
- **Container**: `cloudflare/cloudflared:latest`, runs `cloudflared tunnel --no-autoupdate run`
- **Resources**: 0.25 CPU, 0.5 GB memory, fixed 1 replica (not scaled)
- **Bicep module**: `infra/modules/cloudflared.bicep`

### Route Rules

Routes are configured in the Cloudflare dashboard under **Tunnels > Routes**. All routes are "Published application" type and point to internal Azure Container App FQDNs (not publicly accessible).

| Destination | Target Container App | Purpose |
|---|---|---|
| `claudenest.app/api/*` | Backend (`ca-claudenest-api-{env}`) | REST API endpoints |
| `claudenest.app/hubs/*` | Backend (`ca-claudenest-api-{env}`) | SignalR hub (WebSocket connections) |
| `claudenest.app/install.sh` | Backend (`ca-claudenest-api-{env}`) | Linux/macOS agent install script |
| `claudenest.app/install.ps1` | Backend (`ca-claudenest-api-{env}`) | Windows agent install script |
| `claudenest.app` | Frontend (`ca-claudenest-web-{env}`) | React SPA (catch-all) |

The internal service URLs follow the pattern:
```
https://ca-claudenest-{app}-{env}.internal.{environment-unique-id}.{region}.azurecontainerapps.io
```

### DNS

The `claudenest.app` domain is managed in Cloudflare DNS. Cloudflare automatically creates CNAME records that point to the tunnel when routes are configured.

### Why Cloudflare Tunnel?

- **No public IPs** -- All Azure resources remain fully internal to the VNet
- **DDoS protection** -- Cloudflare's network sits in front of all traffic
- **Simple routing** -- Path-based routing to multiple backend services without needing Azure Application Gateway or Front Door
- **Outbound-only** -- The `cloudflared` container initiates the connection to Cloudflare, so no inbound firewall rules are needed

## Azure Infrastructure

All infrastructure is defined in Bicep (`infra/`) and deployed via GitHub Actions.

### Networking

| Resource | Purpose |
|---|---|
| **VNet** | Isolates all container apps and services on a private network |
| **Infrastructure Subnet** | Hosts the Container Apps Environment |
| **Private Endpoint Subnet** | Private endpoints for SQL, SignalR, Key Vault |
| **Private DNS Zones** | Resolves private endpoint FQDNs within the VNet |
| **cloudflared Container App** | Establishes outbound tunnel to Cloudflare -- the only path in from the internet |

### Compute

| Resource | Purpose | Scaling |
|---|---|---|
| **Backend Container App** | ASP.NET Core API + SignalR Hub | min 1, max 3 replicas |
| **Frontend Container App** | React SPA served via nginx | min 1, max 2 replicas |
| **cloudflared Container App** | Cloudflare Tunnel ingress | fixed 1 replica |

### Data & Messaging

| Resource | Purpose |
|---|---|
| **Azure SQL** (Serverless) | Users, Agents, Sessions, Credentials, Billing |
| **Azure SignalR Service** | Manages WebSocket connections at scale (free tier for dev) |
| **Key Vault** | Stores connection strings, API keys, tunnel tokens |

### Observability

| Resource | Purpose |
|---|---|
| **Log Analytics Workspace** | Centralized logging from all container apps |
| **Application Insights** | Telemetry, traces, metrics from the backend |
| **Availability Tests** | External pings every 5 min from 3 regions (US, AU, UK) |

#### Health Endpoints

| URL | Source | Purpose |
|---|---|---|
| `https://claudenest.app/health` | Frontend (nginx) | Web app availability |
| `https://claudenest.app/api/health` | Backend (ASP.NET) | API readiness (all health checks pass) |
| `https://claudenest.app/api/alive` | Backend (ASP.NET) | API liveness (basic self-check) |
| `https://claudenest.app/api/hubs/nest` | Backend (SignalR) | Hub availability |

### Identity & Auth

| Resource | Purpose |
|---|---|
| **Auth0** | JWT authentication for web users (Google/GitHub social login) |
| **Managed Identity** | Container Apps access to Key Vault, ACR, SQL |
| **Agent Credentials** | Custom hashed secrets stored in DB (agents never touch Auth0) |

### Container Registry

| Resource | Purpose |
|---|---|
| **Azure Container Registry** | Stores backend and frontend Docker images |

Images are tagged with `YYYY.M.D.{run_number}` and `latest`.

## Deployment

### Application Deployment (`.github/workflows/deploy.yml`)

1. Builds backend and frontend Docker images
2. Pushes to Azure Container Registry
3. Updates Container Apps with new image tags via `az containerapp update`

### Infrastructure Deployment (`.github/workflows/deploy-infra.yml`)

1. Deploys Bicep templates (`infra/main.bicep`) to the Azure resource group
2. Orchestrates 12 modules in dependency order

### Agent Releases (`.github/workflows/release-agent.yml`)

1. Publishes AOT binaries for linux-x64, osx-arm64, osx-x64, win-x64
2. Creates a GitHub Release with tag `agent-v{version}`
3. Agents self-update by downloading from GitHub Releases

## Key Design Decisions

- **No terminal streaming** -- `claude remote-control` handles all I/O natively. ClaudeNest is purely a remote launcher/session manager.
- **Cloudflare Tunnel for ingress** -- No public IPs or Azure ingress controllers. All inbound traffic flows through an outbound-only tunnel from cloudflared.
- **Internal VNet** -- All container apps and PaaS services communicate over private endpoints. Nothing is directly internet-facing.
- **Two auth layers** -- Auth0 JWT for web users, custom hashed secrets for agents. Agents connect via SignalR, not HTTP APIs.
- **Agent AOT binaries** -- Single native binary per platform, no runtime dependency. Self-updating via GitHub Releases.
