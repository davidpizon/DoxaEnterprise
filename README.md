# Doxa Enterprise

**Doxa Enterprise** is a multi-tenant, subscription-based SaaS platform for highly regulated
industries (HIPAA / SOC 2 Type II / NIST), built on **.NET Aspire** with Keycloak OIDC single sign-on,
PostgreSQL, Redis, OpenTelemetry, and HTTP resilience.

> **Naming.** "Doxa Enterprise" is the product display name. `Doxa` is the code/namespace prefix
> (`Doxa.AppHost`, `Doxa.Web`, …) and `DoxaEnterprise` is the repository/solution identifier.

## Repository layout

| Path | What's there |
| --- | --- |
| [`src/`](src/README.md) | The .NET Aspire solution (`DoxaEnterprise.slnx`) — AppHost, ServiceDefaults, Web, ApiService, Keycloak realm, and tests. Start here to build and run. |
| [`docs/`](docs/README.md) | Architecture, security, and compliance documentation — specs, plans, AFM, and reverse-engineered product examples. |

## Quick start

```powershell
cd src/Doxa.AppHost
dotnet run
```

See [`src/README.md`](src/README.md) for prerequisites, secret configuration, and cloud-deployment
instructions, and [`docs/README.md`](docs/README.md) for the full documentation index.
