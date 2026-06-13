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

## 2. Multi-Tenant Database Sharding Strategy (Azure SQL Pool Pattern)

To achieve strict HIPAA isolation and SOC 2 data containment, Doxa Enterprise drops standard
shared-table indexing in favor of an Elastic Database Pool Sharding architecture
(Database-per-Tenant pattern). This strategy eliminates noisy-neighbor performance problems and
guarantees precise security limits.

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
  [Azure SQL Multi-Tenant Elastic Database Pool]
  (Dynamic shared allocation of computing capacity / eDTUs)
```

### 2.2 Core Operational Architecture Specs

- **Shard Resource Provisioning:** Databases live within a centralized Azure SQL Elastic Pool.
  Individual subscriber databases scale their compute metrics automatically up to assigned limits
  during high usage periods without impacting adjacent tenant data nodes.
- **Cryptographic Key Isolation (database-level TDE CMK) [^6]:** Each tenant database is encrypted
  with Transparent Data Encryption using a **customer-managed key (the TDE protector) set at the
  *database* level** — not the shared logical-server level — stored in the subscriber's own Azure Key
  Vault or Managed HSM and accessed through a **user-assigned managed identity** (granted `Get`,
  `wrapKey`, and `unwrapKey`). This delivers true per-tenant *key and identity* isolation inside a
  shared elastic pool. Revoking or deleting a departing subscriber's key cryptographically renders
  their underlying data unreadable, and new key versions are auto-rotated within 24 hours.
- **Cross-Tenant Leakage Prevention:** Data queries must flow through an Application Tier routing
  layer that verifies identity matching against Microsoft Entra claims context tokens before opening
  target connections.

### 2.3 Shard Registration Script (Database Automation DDL)

This SQL setup routine executes on the Doxa central routing map coordinator when provisioning a new
enterprise platform subscriber.

```sql
-- 1. Create a secure tracking layout table on the Global Catalog Manager Router instance
CREATE TABLE TenantRoutingCatalog (
    TenantId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    TenantName NVARCHAR(256) NOT NULL,
    AzureSqlDatabaseName NVARCHAR(128) NOT NULL,
    DataIsolationStatus VARCHAR(50) NOT NULL DEFAULT 'ACTIVE'
        CONSTRAINT CK_Tenant_IsolationStatus
        CHECK (DataIsolationStatus IN ('ACTIVE', 'SUSPENDED', 'DEPROVISIONED')),
    CustomerEncryptionKeyUri NVARCHAR(512) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);

-- 2. Enforce a strict one-to-one tenant-to-database mapping so a tenant name or a physical
--    database can never be registered twice (a data-integrity guard, not a visibility control).
CREATE UNIQUE INDEX UX_Tenant_Name ON TenantRoutingCatalog (TenantName);
CREATE UNIQUE INDEX UX_Tenant_Database ON TenantRoutingCatalog (AzureSqlDatabaseName);

-- 3. Stored procedure used by the automated onboarding worker to safely provision shards
GO
CREATE PROCEDURE sp_ProvisionSecureTenantShard
    @TenantName NVARCHAR(256),
    @TargetDatabaseName NVARCHAR(128),
    @KeyVaultUri NVARCHAR(512),
    @NewTenantId UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON; -- Any runtime error reliably dooms and rolls back the transaction.

    -- Validate inputs before touching the catalog.
    IF NULLIF(LTRIM(RTRIM(@TenantName)), '') IS NULL
        OR NULLIF(LTRIM(RTRIM(@TargetDatabaseName)), '') IS NULL
        OR NULLIF(LTRIM(RTRIM(@KeyVaultUri)), '') IS NULL
    BEGIN
        THROW 50001, 'TenantName, TargetDatabaseName, and KeyVaultUri are all required.', 1;
    END;

    SET @NewTenantId = NEWID();

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Insert the tracking identifier safely into the isolation lookup catalog.
        INSERT INTO TenantRoutingCatalog (TenantId, TenantName, AzureSqlDatabaseName, CustomerEncryptionKeyUri)
        VALUES (@NewTenantId, @TenantName, @TargetDatabaseName, @KeyVaultUri);

        -- Note: the application wrapper layer provisions the separate physical Azure SQL database
        -- inside the Elastic Pool via the Azure Resource Manager API immediately after this step,
        -- then assigns the database-level TDE customer-managed key and user-assigned identity.

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW; -- Re-raise the original error to the onboarding worker for handling/retry.
    END CATCH;
END;
GO
```

---

## References

[^1]: <https://betterprogramming.pub>
[^2]: <https://joshua-lucas.com>
[^3]: [Embed Zero Trust security into your developer workflow — GitHub OIDC & Workload Identity Federation](https://learn.microsoft.com/security/zero-trust/develop/embed-zero-trust-dev-workflow) and [Deploy to Azure infrastructure with GitHub Actions](https://learn.microsoft.com/devops/deliver/iac-github-actions)
[^4]: [Use the Bicep linter](https://learn.microsoft.com/azure/azure-resource-manager/bicep/linter) and [Add linter settings in the Bicep config file](https://learn.microsoft.com/azure/azure-resource-manager/bicep/bicep-config-linter)
[^5]: [Bicep what-if: preview changes before deployment](https://learn.microsoft.com/azure/azure-resource-manager/bicep/deploy-what-if)
[^6]: [Transparent data encryption (TDE) with customer-managed keys at the database level](https://learn.microsoft.com/azure/azure-sql/database/transparent-data-encryption-byok-database-level-overview)
