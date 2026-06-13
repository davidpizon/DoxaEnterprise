# Multi-Tenant Continuous Delivery & Data Isolation Architecture Plan

This comprehensive plan covers the automated delivery pipeline for the Azure infrastructure and the
logical-to-physical database tier layout required to guarantee isolation between global enterprise
subscribers.

---

## 1. CI/CD Pipeline: GitHub Actions Workflow for Multi-Environment Infrastructure [^1]

This production-grade GitHub Actions workflow acts as your continuous deployment vehicle. It automates
linting, preflight validation, change preview (what-if), and rolling out of the secure Bicep
infrastructure modules across sequential target environments (`staging` and `production`).[^2] It
authenticates with **OpenID Connect (OIDC)** federation — no long-lived Azure credentials are stored
as GitHub secrets — and gates production behind a protected GitHub environment.

```yaml
name: "Doxa Enterprise: Infrastructure CI/CD"

on:
  push:
    branches:
      - main
    paths:
      - 'infrastructure/**.bicep'
  pull_request:
    branches:
      - main
    paths:
      - 'infrastructure/**.bicep'

# Least-privilege token scope. id-token:write is required for OIDC federation to Azure;
# contents:read is all the workflow needs to check out the templates.
permissions:
  id-token: write
  contents: read

# Serialize infrastructure runs so two deployments never mutate the same state concurrently.
concurrency:
  group: infra-${{ github.ref }}
  cancel-in-progress: false

jobs:
  validate_and_lint:
    name: "1. Lint, Validate & Preview (What-If)"
    runs-on: ubuntu-latest
    timeout-minutes: 15
    steps:
      - name: Checkout Source Code
        uses: actions/checkout@v4 # Hardening: pin to a full commit SHA in production repos

      - name: Azure CLI Login via OIDC (Staging)
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID_STAGING }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID_STAGING }}

      - name: Build & Lint Bicep
        # 'az bicep build' compiles the template and emits Bicep linter diagnostics.
        # Set the relevant analyzer rules to level "error" in bicepconfig.json so that
        # best-practice violations fail this step rather than passing as warnings.
        run: az bicep build --file infrastructure/storage.bicep

      - name: Validate Deployment Configuration (Preflight)
        run: |
          az deployment group validate \
            --resource-group rg-doxa-staging \
            --template-file infrastructure/storage.bicep \
            --parameters location=eastus2

      - name: Preview Changes (What-If)
        run: |
          az deployment group what-if \
            --resource-group rg-doxa-staging \
            --template-file infrastructure/storage.bicep \
            --parameters location=eastus2

  deploy_staging:
    name: "2. Deploy to Staging Environment"
    needs: validate_and_lint
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    timeout-minutes: 30
    environment: staging
    steps:
      - name: Checkout Source Code
        uses: actions/checkout@v4

      - name: Azure CLI Login via OIDC (Staging)
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID_STAGING }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID_STAGING }}

      - name: Deploy Storage to Staging
        uses: azure/arm-deploy@v2
        with:
          subscriptionId: ${{ secrets.AZURE_SUBSCRIPTION_ID_STAGING }}
          resourceGroupName: rg-doxa-staging
          template: infrastructure/storage.bicep
          parameters: 'location=eastus2 retentionPeriodDays=30' # Shorter timeline retention for staging

  deploy_production:
    name: "3. Deploy to Production Environment"
    needs: deploy_staging
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    timeout-minutes: 30
    environment: production # GitHub environment protection: required reviewers must approve the apply
    steps:
      - name: Checkout Source Code
        uses: actions/checkout@v4

      - name: Azure CLI Login via OIDC (Production)
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID_PRODUCTION }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID_PRODUCTION }}

      - name: Preview Production Changes (What-If)
        # Surfaces the exact diff in the run log for the approver to review before the apply.
        run: |
          az deployment group what-if \
            --resource-group rg-doxa-prod \
            --template-file infrastructure/storage.bicep \
            --parameters location=eastus2 retentionPeriodDays=2555

      - name: Deploy Secure Storage to Production
        uses: azure/arm-deploy@v2
        with:
          subscriptionId: ${{ secrets.AZURE_SUBSCRIPTION_ID_PRODUCTION }}
          resourceGroupName: rg-doxa-prod
          template: infrastructure/storage.bicep
          parameters: 'location=eastus2 retentionPeriodDays=2555' # 7 Years structural regulatory window
```

### 1.1 Pipeline Hardening Standards

The workflow above applies the following industry-standard, audit-aligned controls:

- **Passwordless OIDC federation [^3]** — Azure sign-in uses GitHub's OIDC token exchanged for a
  short-lived Azure access token via Microsoft Entra Workload Identity Federation. Configure **three
  federated credentials** on the deployment identity so each trigger context is explicitly scoped:
  one for the `production` **Environment**, one for **Pull Request**, and one for the `main`
  **Branch**. (The PR credential is what lets the `pull_request`-triggered validation job log in.)
- **Bicep linting as a gate [^4]** — keep a `bicepconfig.json` with security/best-practice analyzer
  rules set to level `error`, so `az bicep build` fails the pipeline on violations instead of merely
  warning.
- **What-if before apply [^5]** — every environment previews the resource diff with
  `az deployment group what-if`, and production surfaces the diff in the run log for the human
  approver before the change is applied.
- **Environment protection rules** — the `production` GitHub environment requires reviewer approval
  and is restricted to the `main` branch, so no apply runs unattended.
- **Least privilege & isolation** — workflow `permissions` are minimized (`id-token: write`,
  `contents: read`); staging and production use **separate identities and subscriptions**.
- **Supply-chain hardening** — pin third-party actions to a full commit SHA (not a moving tag),
  set `timeout-minutes` on every job, and use a `concurrency` group to serialize infrastructure runs.

---

## 2. Multi-Tenant Database Sharding Strategy (Azure Database for PostgreSQL Pattern)

To achieve strict HIPAA isolation and SOC 2 data containment, Doxa Enterprise's **production target**
drops standard shared-table indexing in favor of a **database-per-tenant** pattern hosted on a shared
**Azure Database for PostgreSQL Flexible Server**. This strategy eliminates noisy-neighbor performance
problems and guarantees precise security limits.

> **Isolation model — current vs. target.**
>
> - **Now (development):** a single shared **containerized PostgreSQL** database (provisioned by .NET
>   Aspire). Tenant isolation is enforced at the **row level** — a `TenantId` column on every
>   tenant-scoped table plus PostgreSQL **Row-Level Security (RLS)**. This is exactly what the
>   `doxa-enforce-tenant-isolation-clause` SAST rule guards (see
>   [enterprise-resilience-application-security-blueprint.md](enterprise-resilience-application-security-blueprint.md)).
> - **Target (production):** **database-per-tenant** on Azure Database for PostgreSQL Flexible Server,
>   with per-tenant connection routing via the `TenantRoutingCatalog` below. Adopted once the
>   application exits its development cycle.
>
> **Production parallelism assumption (design constraint).** Development decisions **must** assume that
> in production **multiple stateless application pods run in parallel, each resolving its own per-tenant
> database connection** (distinct databases for different tenants). Concretely: keep pods stateless;
> never hold one tenant's data, connection, or `TenantId` in process-global or static state; resolve
> the tenant's connection string per request from the routing catalog; and run schema migrations
> per-tenant-database. Code written against the single development database must stay correct when the
> same container image is fanned out across many pods, each bound to a different tenant database.

### 2.1 Logical-to-Physical Data Separation Model

```text
       [Client Traffic Destination Gateway]
                       │
                       ▼
       [Tenant Mapping Router Engine] ─── (Looks up Tenant ID metadata)
                       │
        ┌──────────────┼──────────────┐
        ▼              ▼              ▼
 ┌──────────────┐┌──────────────┐┌──────────────┐
 │ Tenant_001_DB││ Tenant_002_DB││ Tenant_003_DB│ ──► (Isolated Cryptographic Keys)
 └──────────────┘└──────────────┘└──────────────┘
        ▲              ▲              ▲
        └──────────────┼──────────────┘
                       ▼
  [Shared Azure Database for PostgreSQL Flexible Server]
  (Per-tenant databases; shared vCore compute allocation)
```

### 2.2 Core Operational Architecture Specs

- **Shard Resource Provisioning:** Tenant databases live on a centralized Azure Database for
  PostgreSQL Flexible Server. Compute (vCores / memory) is shared across the per-tenant databases and
  scales with the server tier during high-usage periods without impacting adjacent tenant data nodes.
- **Cryptographic Key Isolation (customer-managed key encryption) [^6]:** Data at rest is encrypted
  with a **customer-managed key (CMK)** stored in the subscriber's own Azure Key Vault or Managed HSM
  and accessed through a **user-assigned managed identity** (granted `Get`, `wrapKey`, and `unwrapKey`).
  Azure Database for PostgreSQL Flexible Server applies the CMK at the **server** level, so true
  per-tenant *key and identity* isolation is realized by hosting each subscriber's database on its own
  CMK-encrypted server (or per-tier server group); the exact isolation granularity is set by the
  data-isolation model. Revoking or deleting a departing subscriber's key cryptographically renders
  their underlying data unreadable, and new key versions are auto-rotated within 24 hours.
- **Cross-Tenant Leakage Prevention:** Data queries must flow through an Application Tier routing
  layer that verifies identity matching against Microsoft Entra claims context tokens before opening
  target connections.

### 2.3 Shard Registration Script (Database Automation DDL)

This SQL setup routine executes on the Doxa central routing map coordinator when provisioning a new
enterprise platform subscriber.

```sql
-- 1. Create a secure tracking layout table on the Global Catalog Manager Router instance.
--    gen_random_uuid() is built into PostgreSQL 13+ (or provided by the pgcrypto extension).
CREATE TABLE TenantRoutingCatalog (
    TenantId UUID NOT NULL PRIMARY KEY DEFAULT gen_random_uuid(),
    TenantName VARCHAR(256) NOT NULL,
    PostgresDatabaseName VARCHAR(128) NOT NULL,
    DataIsolationStatus VARCHAR(50) NOT NULL DEFAULT 'ACTIVE'
        CONSTRAINT CK_Tenant_IsolationStatus
        CHECK (DataIsolationStatus IN ('ACTIVE', 'SUSPENDED', 'DEPROVISIONED')),
    CustomerEncryptionKeyUri VARCHAR(512) NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- 2. Enforce a strict one-to-one tenant-to-database mapping so a tenant name or a physical
--    database can never be registered twice (a data-integrity guard, not a visibility control).
CREATE UNIQUE INDEX UX_Tenant_Name ON TenantRoutingCatalog (TenantName);
CREATE UNIQUE INDEX UX_Tenant_Database ON TenantRoutingCatalog (PostgresDatabaseName);

-- 3. Function used by the automated onboarding worker to safely provision shards.
--    A plpgsql function runs inside the caller's transaction, so any raised error aborts
--    and rolls back the insert automatically (no explicit BEGIN/COMMIT/ROLLBACK needed).
CREATE OR REPLACE FUNCTION sp_ProvisionSecureTenantShard(
    p_TenantName          VARCHAR(256),
    p_TargetDatabaseName  VARCHAR(128),
    p_KeyVaultUri         VARCHAR(512)
)
RETURNS UUID
LANGUAGE plpgsql
AS $$
DECLARE
    v_NewTenantId UUID := gen_random_uuid();
BEGIN
    -- Validate inputs before touching the catalog.
    IF NULLIF(BTRIM(p_TenantName), '') IS NULL
        OR NULLIF(BTRIM(p_TargetDatabaseName), '') IS NULL
        OR NULLIF(BTRIM(p_KeyVaultUri), '') IS NULL THEN
        RAISE EXCEPTION 'TenantName, TargetDatabaseName, and KeyVaultUri are all required.'
            USING ERRCODE = 'check_violation';
    END IF;

    -- Insert the tracking identifier safely into the isolation lookup catalog.
    INSERT INTO TenantRoutingCatalog (TenantId, TenantName, PostgresDatabaseName, CustomerEncryptionKeyUri)
    VALUES (v_NewTenantId, p_TenantName, p_TargetDatabaseName, p_KeyVaultUri);

    -- Note: the application wrapper layer provisions the separate physical PostgreSQL database on the
    -- shared Azure Database for PostgreSQL Flexible Server via the Azure Resource Manager API
    -- immediately after this step, then assigns the customer-managed key and user-assigned identity.

    RETURN v_NewTenantId;
END;
$$;
```

---

## References

[^1]: [Deploy Bicep files by using GitHub Actions](https://learn.microsoft.com/azure/azure-resource-manager/bicep/deploy-github-actions) — Microsoft Learn guide to the GitHub Actions → Azure infrastructure deployment workflow.
[^2]: [Using environments for deployment](https://docs.github.com/actions/deployment/targeting-different-environments/using-environments-for-deployment) — GitHub Docs on staged environments and deployment protection rules (required reviewers, branch restrictions).
[^3]: [Embed Zero Trust security into your developer workflow — GitHub OIDC & Workload Identity Federation](https://learn.microsoft.com/security/zero-trust/develop/embed-zero-trust-dev-workflow) and [Deploy to Azure infrastructure with GitHub Actions](https://learn.microsoft.com/devops/deliver/iac-github-actions)
[^4]: [Use the Bicep linter](https://learn.microsoft.com/azure/azure-resource-manager/bicep/linter) and [Add linter settings in the Bicep config file](https://learn.microsoft.com/azure/azure-resource-manager/bicep/bicep-config-linter)
[^5]: [Bicep what-if: preview changes before deployment](https://learn.microsoft.com/azure/azure-resource-manager/bicep/deploy-what-if)
[^6]: [Data encryption with customer-managed keys — Azure Database for PostgreSQL Flexible Server](https://learn.microsoft.com/azure/postgresql/flexible-server/concepts-data-encryption)
