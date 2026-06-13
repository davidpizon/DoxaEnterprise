# Recreation Prompt — "TrustedData": An Enterprise-Grade, Multi-Client MDM Platform with an MCP Server for AI

> **How to use this file.** This is a single, self-contained build prompt. Hand it to a capable
> coding agent (or engineering team) to recreate a product equivalent to **Profisee** and its
> **MCP Server** (https://profisee.com/platform/mcp-server/) — an AI-first **Master Data Management
> (MDM)** platform whose flagship AI surface is a **Model Context Protocol (MCP) Server**: a secure,
> governed, two-way bridge that gives Claude, Copilot, ChatGPT, Foundry, and any MCP-compatible tool
> real-time access to matched, merged, and validated **golden-record** master data. The prompt is
> deliberately exhaustive: vision, personas, the MDM core engine, matching/survivorship, data
> quality & governance, stewardship, the MCP server tool surface, the Aisey-style AI assistant,
> integrations, security/compliance, multi-tenancy, data model, deployment, and acceptance criteria.
>
> Reference product studied (June 2026): Profisee MDM Platform + Profisee MCP Server. This is an
> independent recreation spec, not affiliated with Profisee.

---

## 0. Role & Objective

You are a principal data-platform architect. Build **TrustedData** — a production-ready, cloud-scale,
**multi-tenant (multi-client) SaaS** Master Data Management platform that unifies customer, product,
supplier, or any entity data into governed **golden records**, and exposes those records to AI agents
through a **governed MCP Server**.

Core promises to preserve:
- *"Trusted Master Data. Available to Any AI."* / *"Connect AI to Trusted Data Platform."*
- *"Your AI tools are only as good as the data behind them"* — ground AI in verified master data to
  eliminate hallucination.
- *"Do less work managing master data"* — automate matching, rules, and stewardship.
- **Governed by default**, with an **audit trail on every action**; **self-service** ("No SQL. No IT
  queue").

The platform has three layers:
- **MDM Core** — ingest, model, match, survive, validate, govern, and steward master data.
- **MCP Server** — a secure, **two-way** governed bridge from MCP-compatible AI tools to that data.
- **AI Assistant ("Aisey"-style)** — agentic MDM: data-quality agents, semantic matching, and
  natural-language record interaction.

---

## 1. The Problem (design rationale)

1. **Data silos block AI access** — disparate sources create conflicting versions of the truth.
2. **Disconnected data causes hallucination** — AI guesses without verified records.
3. **MDM ROI stays locked away** — golden records never reach the AI workflows that need them.

TrustedData solves these by connecting AI directly to governed master data with full auditability.

---

## 2. Target Audiences

| Persona | Need | What TrustedData gives them |
| --- | --- | --- |
| **Data leaders (CDO)** | Align MDM with business objectives | Multidomain governance, lineage, audit, ROI metrics |
| **Business leaders** | Fast ROI, self-service answers | NL queries over golden data via AI — no IT intermediary |
| **Practitioners / stewards** | Simpler stewardship | Intuitive UI, match review, exception routing, AI agents |
| **Microsoft field sellers / partners** | Fabric-native MDM | OneLake/Fabric integration, one-click MDS-style migration |

**Industries:** manufacturing (resilient supply chains), healthcare (patient care), financial
services (risk reduction).

---

## 3. High-Level Architecture

```
   AI Clients (Claude / Copilot / ChatGPT / Foundry / any MCP tool)   Power Platform / Copilot
                         │  MCP (OAuth, SSO-fronted, audited)                   │
                         ▼                                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│  MCP SERVER (governed, two-way bridge)                                                     │
│  • exposes MDM as MCP tools (search, get golden record, propose/apply change, match, audit)│
│  • enforces per-tenant RBAC + governance rules; every call logged & traceable              │
│  • read AND write-back (steward edits, exception resolution) within AI tools               │
└───────────────┬────────────────────────────────────────────────────────────────────────────┘
                ▼
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│  MDM CORE                                                                                  │
│  Modeling (domains/entities/attributes) → Ingestion/Integration (REST, webhooks, batch,    │
│  Fabric Open Mirroring) → Data Quality (validation rules) → Matching (fuzzy + AI semantic) │
│  → Survivorship (golden record build) → Governance (policy, lineage, audit) → Stewardship  │
│  (review UI, workflows, exception routing) → Publish/Sync (real-time to downstream systems) │
└───────────────┬───────────────────────────────────────────────┬────────────────────────────┘
                ▼                                                 ▼
   ┌─────────────────────────┐                      ┌──────────────────────────────────┐
   │ AI ASSISTANT (Aisey-like)│                      │ INTEGRATION FABRIC                │
   │ • data-quality agents    │                      │ OneLake/Fabric, Azure OpenAI,     │
   │ • semantic matching      │                      │ Purview, Databricks, Power        │
   │ • NL record interaction  │                      │ Platform, REST/webhooks, MDS migr.│
   │ • agents · skills · chat │                      └──────────────────────────────────┘
   └─────────────────────────┘
```

---

## 4. MDM Core — the engine

### 4.1 Multidomain modeling
- Configurable **domains/entities** for **customer, product, supplier, or any** data; user-defined
  attributes, data types, reference data, and **relationships** (hierarchies, associations).
- A no-code/low-code **model designer** for entities, attributes, validation, and match/survivorship
  config.

### 4.2 Integration & ingestion
- **Real-time integration with REST APIs and webhooks**; plus batch loads and streaming.
- **Source-record store** preserving each system's version; **crosswalk/cross-reference** mapping
  every source record to its golden record.
- **Fabric Open Mirroring** + **OneLake** sync so data flows bronze → governed golden master without
  leaving the lakehouse.

### 4.3 Matching & survivorship (golden records)
- **Entity resolution** with **explainable matching**: automatic **fuzzy matching** of potential
  duplicates plus **AI-powered semantic matching**.
- Configurable **matching strategies** (blocking keys, weighted attribute comparison, thresholds for
  auto-merge vs. review).
- **Survivorship rules** that selectively populate the golden record with the best field values
  across all source systems (e.g., most-recent, most-trusted-source, most-complete).
- Output: a **golden/master record** per real-world entity, deduplicated and authoritative, with full
  provenance back to contributing sources.

### 4.4 Data quality & governance
- **Validation rules** keep data accurate, governed, and AI-ready; **real-time data quality flagging**.
- **Governance**: policy enforcement, **lineage**, and an **audit trail on every action**.
- **Address matching & verification** (standardize/validate postal addresses).
- Three operating pillars: **Get your data clean** (smarter matching) → **Keep it that way**
  (automated rules + workflows that prevent issues) → **Share it everywhere** (real-time sync).

### 4.5 Stewardship & workflow
- A **modern, intuitive stewardship UI** for review, merge/unmerge, edit, and approval.
- **Visually automated workflows** for business processes; **exception routing and identification**;
  task queues for stewards.

### 4.6 Publish & sync
- **Seamless, real-time sync** of golden records across systems and data platforms (downstream
  publish, change-data-capture, subscriptions/webhooks).

---

## 5. MCP Server — the flagship AI surface

The MCP Server implements the open Model Context Protocol as **a secure, governed bridge** giving any
MCP-compatible AI **real-time, two-way access** to matched, merged, and validated master data.

### 5.1 Principles
- **Any MCP-compatible AI** (Claude, Copilot, ChatGPT, Foundry, agents).
- **Governed by default** — every action is logged and traceable; RBAC + governance rules apply to
  AI exactly as they do to humans.
- **Self-service instantly** — natural-language querying, **no SQL, no IT queue**.
- **Two-way** — read *and* write-back (steward edits, exception resolution) from within AI tools and
  developer workflows.

### 5.2 MCP tool surface (design these tools)
- `list_domains` / `list_entities` / `describe_entity` — discover the governed model.
- `search_master_data(entity, naturalLanguageOrFilter)` — query golden records in NL or structured
  filters; returns trusted, contextual records.
- `get_golden_record(entity, id)` — fetch an authoritative record with provenance.
- `get_record_sources(entity, id)` — show contributing source records + survivorship decisions
  (explainability).
- `get_match_candidates(entity, id|payload)` — return potential duplicates with match scores/reasons.
- `propose_change(entity, id, changes)` / `apply_change(...)` — write-back with governance checks +
  steward routing (the "two-way" capability).
- `resolve_exception(taskId, decision)` — drive stewardship automation/exception routing.
- `run_data_quality_check(entity, id|scope)` — surface governance/quality gaps in real time.
- `get_audit_trail(entity, id|scope)` — return the immutable action log.

### 5.3 Worked use cases the server must support
- **Customer intelligence:** "Who are our top strategic customers and what is our risk exposure?" →
  grounded summary across N accounts, concentration-risk detection.
- **Product data:** "Which products are discontinued but still have open purchase orders?" → live
  cross-reference of product master data.
- **Supplier risk:** "Which suppliers have incomplete compliance attributes or missing
  certifications?" → governance gaps surfaced from supplier master data.
- **Financial risk:** "What is our total credit exposure by region, and which accounts are on NET180
  terms?" → CFO-ready analysis in seconds.
- **Agentic workflows:** AI agents access trusted master data autonomously to power stewardship
  automation, exception routing, and multi-step workflows without manual data prep.
- **Self-service analytics:** business leaders get on-demand answers from governed data — no IT
  intermediary, no stale export.

### 5.4 Security on the bridge
- SSO-fronted, OAuth-protected MCP endpoints; per-tenant isolation; tool/attribute-level
  authorization; redaction of unauthorized fields; full audit of every AI read and write.

---

## 6. AI Assistant ("Aisey"-style, agentic MDM)

- **Agents, skills, and chat** built into the platform.
- **Data Quality Agent** that automatically resolves attribute-level quality issues as they are
  detected.
- **AI-powered semantic matching** to augment deterministic/fuzzy matching.
- **Natural-language record interaction** for stewards and business users.
- Together with the MCP Server, this forms an AI foundation spanning **every stage of the master data
  lifecycle**.

---

## 7. Integrations (build connectors/adapters)

- **Microsoft Fabric** — native, including **OneLake** and **Open Mirroring**.
- **Azure OpenAI / Foundry** — AI readiness at scale; model access for the assistant.
- **Microsoft Purview** — unified data governance alongside MDM.
- **Power Platform & Copilot** — agentic workflows and connectivity.
- **Azure Databricks** — high-quality data for trustworthy analytics.
- **Master Data Services (MDS)** — one-click migration path.
- **REST APIs + webhooks** — real-time inbound/outbound integration.

---

## 8. Security, Compliance & Trust

- **SOC 2 (AICPA-SOC)**, **ISO 27001**, **HIPAA/HITECH** compliance; design toward re-certification.
- **Encryption at rest and in flight**; third-party auditor certified.
- Per-tenant data isolation; RBAC across users, stewards, and AI agents; immutable audit logging;
  data lineage end to end.

---

## 9. Multi-Tenancy & Deployment

- First-class **Organization (client/tenant)** entity; strict isolation of model, data, credentials,
  and audit across tenants.
- **Deployment flexibility:** "the only MDM available as **SaaS, PaaS, IaaS, hybrid, or on-prem**."
- **Cloud support:** Microsoft Azure, AWS, Google Cloud.
- Containerized, horizontally scalable services; IaC; regional data residency.

---

## 10. Recommended Tech Stack (adapt as needed)

- **MDM core:** scalable services (.NET / JVM / Go); a relational store (Postgres/SQL Server) for
  master + crosswalk; a search/index tier (Elasticsearch/OpenSearch) for matching candidates; a
  lakehouse sync layer for Fabric/OneLake.
- **Matching:** blocking + weighted comparison engine; embeddings + vector index for semantic
  matching; an explainability layer (per-pair scores and reasons).
- **MCP Server:** a service implementing MCP (streamable HTTP), exposing the tool surface in §5.2,
  with OAuth/SSO, RBAC, redaction, and audit emission.
- **AI assistant:** orchestrator calling Azure OpenAI/Foundry; agent + skill framework; NL→query
  translation grounded strictly in the governed model.
- **Stewardship UI:** modern SPA (React/Next or Blazor); workflow designer; match-review screens.
- **Platform:** multi-tenant API gateway, KMS-backed secrets, OpenTelemetry observability.

---

## 11. Core Data Model (minimum)

`Tenant`, `Domain`, `EntityType` (+ `Attribute`, `ReferenceData`, `Relationship`), `SourceSystem`,
`SourceRecord`, `Crosswalk` (source↔golden), `MatchGroup` (+ `MatchPair` with score/explanation),
`GoldenRecord` (+ `SurvivorshipDecision` per attribute), `ValidationRule`, `GovernancePolicy`,
`StewardshipTask` (+ `Exception`), `Workflow`, `AuditEvent`, `User`, `Role`, `AgentIdentity`,
`McpToolInvocation`, `AiAgent` / `Skill`.

---

## 12. Required Deliverables

1. **MDM core**: model designer, ingestion (REST/webhooks/batch + Fabric Open Mirroring), source +
   crosswalk store, matching engine (fuzzy + semantic, explainable), survivorship/golden-record
   builder, validation rules, governance + lineage + audit, stewardship UI + workflows, real-time
   publish/sync.
2. **MCP Server** implementing the tool surface in §5.2 with **two-way** read/write, SSO/OAuth, RBAC,
   field-level authorization/redaction, and full audit — connectable from Claude/Copilot/ChatGPT/
   Foundry.
3. **AI assistant** with data-quality agent, semantic matching, and NL record interaction.
4. **Integrations**: Fabric/OneLake, Azure OpenAI/Foundry, Purview, Power Platform/Copilot,
   Databricks, MDS migration, REST/webhooks.
5. **Multi-tenant platform** with SaaS/PaaS/IaaS/hybrid/on-prem deployment artifacts and Azure/AWS/GCP
   support.
6. **Security/compliance surfaces**: encryption, RBAC, audit, lineage, trust center, SOC2/ISO/HIPAA
   evidence export.
7. **Docs**: AI-client connection guides, model/match/survivorship authoring, stewardship guide,
   API + MCP tool reference, security/architecture whitepaper.

---

## 13. Acceptance Criteria (definition of done)

- [ ] Ingesting duplicate records from multiple sources yields a single **golden record** built by
      **survivorship rules**, with provenance to each source and an **explainable** match.
- [ ] An MCP-compatible AI connects to the MCP Server, completes SSO/OAuth, and answers a NL question
      (e.g., the customer-intelligence query) grounded **only** in governed master data — no SQL.
- [ ] The MCP Server supports **write-back**: an AI-proposed change is governance-checked, routed to a
      steward (or auto-applied per policy), and recorded in the audit trail.
- [ ] Every read and write (human or AI) produces an immutable, queryable **audit event**; lineage is
      traceable end to end.
- [ ] Matching combines deterministic/fuzzy and **AI semantic** matching, with reasons surfaced for
      each candidate pair.
- [ ] Data-quality issues are flagged in real time and the **data-quality agent** can auto-resolve
      attribute-level issues.
- [ ] Strict tenant isolation across model, data, credentials, and audit; RBAC applies identically to
      users, stewards, and AI agents; field-level redaction works for unauthorized attributes.
- [ ] Golden records sync in real time to downstream systems; Fabric/OneLake mirroring works.
- [ ] The platform deploys as SaaS and as a self-hosted/VPC artifact; encryption at rest and in flight
      is demonstrable.

---

## Sources

- [Profisee MCP Server — Connect AI to Trusted Data Platform](https://profisee.com/platform/mcp-server/)
- [Profisee Platform overview](https://profisee.com/platform/)
- [Golden Record Management](https://profisee.com/platform/golden-record-management/)
- [Profisee brings end-to-end MDM into Microsoft Fabric at FabCon 2026 (+ MCP Server)](https://profisee.com/press-release/profisee-brings-end-to-end-master-data-management-fully-into-microsoft-fabric-at-fabcon-2026-adds-fabric-open-mirroring-support-and-copilot-connectivity-via-profisee-mcp-server/)
- [Profisee 2026.R1 — AI Agents, Semantic Matching & Lakehouse](https://profisee.com/blog/profisee-announces-new-release-2026-r1/)
- [Microsoft Purview and Profisee MDM (Microsoft Learn)](https://learn.microsoft.com/en-us/purview/data-governance-master-data-management-profisee)
- [Master data management with Profisee and Azure Data Factory (Microsoft Learn)](https://learn.microsoft.com/azure/architecture/databases/architecture/profisee-master-data-management-data-factory)
