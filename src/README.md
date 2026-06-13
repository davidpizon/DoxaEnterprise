# Doxa Enterprise

Cloud-scale enterprise platform built on **.NET Aspire 13.4** with **Keycloak** OIDC single sign-on,
PostgreSQL, Redis distributed cache, OpenTelemetry, and HTTP resilience. Projects use the `Doxa.*`
namespace prefix; the solution file is `DoxaEnterprise.slnx`.

## Projects

| Project                  | Role                                                             |
| ------------------------ | --------------------------------------------------------------- |
| `Doxa.AppHost`           | Aspire orchestrator — wires services, DB, cache, and Keycloak.  |
| `Doxa.ServiceDefaults`   | Shared telemetry, health checks, service discovery, resilience. |
| `Doxa.Web`               | Frontend (Blazor Server) — OIDC confidential client.            |
| `Doxa.ApiService`        | Stateless backend API — JWT bearer validation.                  |
| `realms/doxa-realm.json` | Keycloak realm import (clients, roles, scopes).                 |

## Prerequisites

- .NET 10 SDK
- A container runtime (Docker Desktop or Podman) for Postgres, Redis, and Keycloak

## Secret configuration (required before first run)

Secrets are passed as Aspire parameters — never hardcoded. In development, store them in the
AppHost user-secrets store. The realm import reads `DOXA_WEB_CLIENT_SECRET` for the `doxa-web` client,
so it must match the `doxa-web-client-secret` parameter.

```powershell
cd Doxa.AppHost
dotnet user-secrets set "Parameters:postgres-password"        "<dev-password>"
dotnet user-secrets set "Parameters:keycloak-admin"           "admin"
dotnet user-secrets set "Parameters:keycloak-admin-password"  "<dev-password>"
dotnet user-secrets set "Parameters:doxa-web-client-secret"   "<dev-client-secret>"
```

In Staging/Production these map to environment variables (`Parameters__postgres-password`, etc.) or
Azure Key Vault references — see the Cloud Deployment section.

## Run

```powershell
dotnet run --project Doxa.AppHost
```

The Aspire dashboard launches and shows every resource, its logs, traces, and metrics.

## Cloud deployment

- **Azure (AZD / Bicep):** `azd init` then `azd up`. Swap `AddPostgres`/`AddRedis` for
  `AddAzurePostgresFlexibleServer`/`AddAzureRedis` to provision managed services; secret parameters
  bind to Key Vault.
- **Kubernetes (Aspirate):** `aspire publish` produces `aspire-manifest.json`; `aspirate generate`
  emits K8s manifests with secret parameters as K8s Secrets.

## Multi-tenancy & deployment assumptions

- **Now (development):** a single shared PostgreSQL database. Tenant isolation is enforced at the
  **row level** — a `TenantId` column plus PostgreSQL Row-Level Security (RLS).
- **Target (production):** **database-per-tenant** on Azure Database for PostgreSQL Flexible Server.
  Assume **multiple stateless pods running in parallel, each bound to a distinct per-tenant database**.
  So: keep services stateless, never hold a tenant's data/connection/`TenantId` in process-global or
  static state, resolve the per-tenant connection per request, and run migrations per-tenant-database.
  Code written against the single dev database must stay correct when fanned out across many
  per-tenant pods. See
  [docs/plan/multi-tenant-cicd-data-isolation-architecture-plan.md](../docs/plan/multi-tenant-cicd-data-isolation-architecture-plan.md).

## Notes

- The `NU1903` MessagePack warning comes transitively from the Aspire dashboard inside the AppHost
  (dev-time tooling only); it is not shipped in any Doxa service image.
