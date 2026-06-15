# 02 — Company Description

← [Index](00-README.md)

## Mission & vision

**Mission.** Make AI safe to adopt in the world's most regulated and security-conscious organizations by
giving every AI interaction with enterprise data trustworthy context **and** a tamper-evident record.

**Vision.** Doxa becomes the **trust layer** that every regulated enterprise and government agency runs
between its data estate and its AI — the system of record for *what AI was allowed to see, and why*.

## What we sell

Doxa is a compliance-grade **data & AI governance platform**: data catalog, lineage, business glossary,
classification, policy/access control, and "context for AI" (activation via MCP server, SQL, API, SDK) —
all built on an immutable audit ledger and sovereign-by-default deployment. Positioning, modeled on
[Atlan](https://atlan.com/) and repositioned for regulated buyers: **"The Trust Layer for Enterprise AI."**

## The origin insight

Doxa did not start as a catalog. It started as a **regulated-SaaS compliance architecture** — immutable
WORM auditing, cryptographic-shred offboarding, database-per-tenant isolation, zero-trust, multi-region DR.
Building that, we recognized the same substrate is exactly what is missing from today's AI-governance tools:
they can describe data, but they cannot *prove* to a regulator what an AI agent accessed. So we are building
the governance product **on top of a compliance spine** that competitors would have to retrofit.

## What exists today vs. what we're building

| Today (real, in-repo) | Building (with the raise) |
|---|---|
| .NET 10 / Aspire multi-service scaffold (Web, API, AppHost) | Data catalog + active-metadata ingestion |
| Keycloak OIDC identity governance (roles, audience-scoped tokens) | Lineage & impact analysis engine |
| Documented compliance architecture: WORM audit, crypto-shred, DB-per-tenant, zero-trust, multi-region DR | Policy / access engine (persona + purpose) |
| SOC 2 / HIPAA / NIST control mappings ([spec](../spec/doxa-enterprise-architecture-compliance-spec.md)) | **AI Governance** (which agents/models may touch which context) |
| `Tenant(Id, Name)` entity + stub API endpoints | **Context activation** (MCP server, SQL, API, SDK) |
| CI/CD, SAST/SCA, Sentinel SIEM, incident-response playbooks (specs) | **Immutable Audit Ledger** as a product surface |

*The left column is the moat; the right column is the product the raise funds.*

## Legal & corporate

- **Structure:** Delaware C-Corp (standard for venture financing).
- **HQ:** `[PLACEHOLDER: city, state]`, US; remote-first.
- **Fiscal year:** calendar.
- **Cap table:** founders + option pool; pre-seed via SAFE (`[PLACEHOLDER: post-money cap]`). See [09](09-financial-plan.md).
- **IP:** all IP assigned to the company; trade-secret + provisional-patent strategy (see [04 §IP](04-product.md#intellectual-property)).

## Company stage

**Pre-seed, pre-product.** We have infrastructure + specifications and are raising to build the MVP and land
design partners. See the honest stage framing throughout [01](01-executive-summary.md) and [09](09-financial-plan.md).

## Values

- **Sovereign by default** — the customer's data and keys stay under the customer's control.
- **Audit everything** — if it happened, there is a signed, immutable record of it.
- **Human-in-the-loop trust** — AI drafts; people certify; regulators can verify.
- **Control-mapped by construction** — compliance is designed in, not bolted on.
