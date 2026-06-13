# ADR 0001 — Documentation Reconciliation: Data Tier, Tenant Isolation, DR, TLS

- **Status:** Accepted
- **Date:** 2026-06-13
- **Scope:** Documentation and architecture plans under `docs/` and the `README` files. No application
  code changed.

## Context

A cross-document audit of all project markdown surfaced seven contradictions/inconsistencies — most
materially, the running .NET Aspire solution uses **PostgreSQL** while every Azure architecture,
compliance, CI/CD, and governance document described **Azure SQL Database**. The decisions below
reconcile the docs and record the durable intent so the divergence does not recur.

## Decisions

1. **Data tier — PostgreSQL now, Azure PostgreSQL later.** The platform uses **containerized
   PostgreSQL** (via .NET Aspire) during the development cycle and migrates to **Azure Database for
   PostgreSQL Flexible Server** (zone-redundant HA + read replicas) for production. All "Azure SQL
   Database / Elastic Pool / TDE" references were rewritten to the PostgreSQL equivalents, including
   converting the tenant-catalog DDL from T-SQL to PL/pgSQL.

2. **Tenant isolation — phased.** *Now (dev):* a single shared PostgreSQL database with **row-level**
   isolation (`TenantId` column + PostgreSQL Row-Level Security), which the `doxa-enforce-tenant-
   isolation-clause` SAST rule guards. *Target (prod):* **database-per-tenant** with per-tenant
   connection routing via the `TenantRoutingCatalog`. The `TenantId` predicate remains a
   defense-in-depth backstop for shared catalog/global tables in production.

3. **Production parallelism (design constraint).** Development decisions **must** assume that in
   production **multiple stateless application pods run in parallel, each bound to a distinct
   per-tenant database**. Keep pods stateless; never hold a tenant's data/connection/`TenantId` in
   process-global or static state; resolve the per-tenant connection per request; run migrations
   per-tenant-database.

4. **Disaster recovery — active-passive priority failover.** Azure Front Door routes to `East US 2`
   (priority 1, primary) with `Central US` (priority 2) as the health-probe-driven failover target.
   The earlier "Active-Active" wording in the compliance spec was corrected to match the resilience
   blueprint.

5. **TLS baseline — 1.3 everywhere.** Per compliance spec §3.1, all storage and compute (including the
   incident-response Logic App and its storage account) enforce **TLS 1.3**; TLS 1.0–1.2 are not
   accepted.

6. **Tenant lifecycle status values.** The catalog's `DataIsolationStatus` is constrained to
   `ACTIVE` / `SUSPENDED` / `DEPROVISIONED`. Offboarding sets the terminal **`DEPROVISIONED`** value
   (the prior `DEACTIVATED` violated the `CHECK` constraint).

7. **Product naming.** Display name is **"Doxa Enterprise."** `Doxa` is the code/namespace prefix
   (`Doxa.AppHost`, …) and `DoxaEnterprise` is the repository/solution identifier.

## Consequences

- The documentation set is internally consistent and matches the running stack for the development
  phase, with the production target explicitly labeled throughout.
- When the application exits its development cycle, the migration work (managed Azure PostgreSQL,
  database-per-tenant provisioning, per-tenant connection routing) is already specified in
  `docs/plan/multi-tenant-cicd-data-isolation-architecture-plan.md`.
- Files touched: `README.md`, `src/README.md`, `docs/README.md`,
  `docs/spec/doxa-enterprise-architecture-compliance-spec.md`,
  `docs/plan/multi-tenant-cicd-data-isolation-architecture-plan.md`,
  `docs/plan/enterprise-governance-security-operations-plan.md`,
  `docs/plan/automated-incident-response-playbook.md`,
  `docs/plan/enterprise-resilience-application-security-blueprint.md`.
