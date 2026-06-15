# 04 — Product

← [Index](00-README.md)

## Vision

**Doxa — The Trust Layer for Enterprise AI.** A compliance-grade data & AI governance platform that unifies
metadata, governs access, activates context to AI, and records every interaction in a tamper-evident ledger.
Modeled on [Atlan](https://atlan.com/)'s layered platform, with a fifth, differentiating layer Atlan lacks:
a **Compliance & Sovereignty substrate**.

## Capability map (layers)

| Layer | Capabilities | Doxa difference |
|---|---|---|
| **Foundation** | Catalog, active metadata, search/discovery, business glossary, classification/PII detection | Parity with catalog incumbents |
| **Intelligence** | Automated lineage & impact analysis, data quality, certification workflows | Parity, AI-native |
| **AI / Context** | "Context for AI": context agents (draft descriptions/terms/metrics), context engineering | Parity with Atlan |
| **Activation** | MCP server, SQL interface, REST API, SDK, reverse metadata | Parity, governed |
| **Compliance & Sovereignty (Doxa-only)** | **Immutable signed audit ledger**, **DB-per-tenant isolation**, **CMK encryption**, **crypto-shred offboarding**, **sovereign/air-gapped deployment**, **control-mapping (SOC 2/HIPAA/NIST)** | **The moat** |

### The fifth context layer
Atlan models four context layers (user, knowledge, meaning, data). Doxa adds a fifth: **provenance / audit
context** — *trace every AI answer to an immutable, signed record.*

## Architecture (centerpiece)

The product fuses an Atlan-style governance platform with Doxa's existing compliance pipeline: **every layer
emits to an immutable, signed audit ledger.**

```mermaid
flowchart TB
  subgraph Edge["Zero-Trust Edge (TLS 1.3)"]
    AFD[Front Door + WAF]
    IDP[Entra ID / Keycloak OIDC<br/>MFA, conditional access]
  end

  subgraph Plane["Doxa Control Plane (AKS)"]
    GW[API / Activation Gateway]
    CAT[Catalog + Active Metadata]
    LIN[Lineage Engine]
    POL[Policy / Access Engine<br/>persona + purpose]
    AIG[AI Governance<br/>agent/model access]
    AGENTS[Context Agents]
    KV[Key Vault — AES-256 CMK<br/>90-day rotation]
  end

  subgraph Data["Data Plane"]
    PG[(PostgreSQL<br/>DB-per-tenant + CMK)]
    GRAPH[(Graph / search / vector<br/>metadata + embeddings)]
  end

  subgraph Activation["Activation"]
    MCP[MCP Server]
    SQLI[SQL]
    API[REST API / SDK]
  end

  subgraph Audit["Immutable Audit Boundary (NIST AU)"]
    HUB[Event Hubs]
    SIGN[SHA-256 signing engine]
    WORM[(WORM Blob<br/>time-based retention + legal hold)]
  end

  AGENTS_CLIENTS[AI clients / agents<br/>Claude, Cursor, custom] --> AFD --> IDP --> GW
  GW --> POL --> AIG
  GW --> CAT & LIN & AGENTS
  CAT & LIN & POL --> PG & GRAPH
  CAT & KV --- PG
  GW --> MCP & SQLI & API
  GW & POL & AIG & CAT & LIN -->|signed event| HUB --> SIGN --> WORM
```

*Compliance details grounded in [`../spec/doxa-enterprise-architecture-compliance-spec.md`](../spec/doxa-enterprise-architecture-compliance-spec.md).*

## Feature set / modules

| Module | What it does |
|---|---|
| Catalog & active metadata | Ingest, unify, search, discover assets across the estate |
| Lineage & impact analysis | Asset+process lineage (incl. column-level), upstream/downstream impact |
| Business glossary | Terms/categories linked to assets; meaning for humans + AI |
| Classification / PII detection | Auto-detect sensitive data; propagate tags along lineage |
| Policy & access | Persona (who) + purpose (why) policies; allow/deny, masking |
| **AI Governance** | Which agents/models may access which context; guardrails; logged decisions |
| **Context activation** | MCP server, SQL, REST API, SDK; reverse metadata into BI/Slack |
| **Immutable Audit Ledger** | Signed, WORM, regulator-presentable record of every access/decision |
| **Sovereign deployment** | Single-tenant / air-gapped / in-region for gov & highest-sensitivity |

## Key runtime flows

**AI access with signed audit (the differentiator):**
```mermaid
sequenceDiagram
  participant A as AI Agent / Client
  participant GW as Activation Gateway (MCP)
  participant POL as Policy + AI Governance
  participant CTX as Catalog / Context
  participant AUD as Audit Ledger (Event Hubs→SHA-256→WORM)
  A->>GW: Request context (definitions/lineage)
  GW->>POL: Authorize (persona, purpose, agent/model)
  POL-->>GW: allow (+ scope)
  GW->>CTX: Fetch governed context
  CTX-->>A: Trusted, contextual response
  GW->>AUD: Emit signed, immutable access record
```

**Certified tenant offboarding (cryptographic shred):**
```mermaid
sequenceDiagram
  participant ADM as Tenant admin
  participant SYS as Doxa
  participant KV as Key Vault (CMK)
  participant AUD as Audit Ledger
  ADM->>SYS: Offboard tenant
  SYS->>KV: Destroy tenant data-encryption key
  Note over SYS,KV: Data becomes cryptographically unrecoverable
  SYS->>AUD: Emit signed destruction attestation
```

## Technical stack

| Layer | Technology |
|---|---|
| Orchestration | .NET 10 + **.NET Aspire** |
| Frontend | Blazor Server (web console) |
| Identity | **Keycloak OIDC** (dev) / Microsoft Entra ID (prod), audience-scoped tokens |
| Data | **PostgreSQL** — DB-per-tenant (prod) / RLS (dev); Redis cache |
| Metadata substrate (to build) | Graph + search + vector indexes |
| Cloud | Azure: **AKS**, PostgreSQL Flexible Server, Front Door + WAF, Event Hubs, **Key Vault (AES-256 CMK, 90-day rotation)**, **immutable WORM Blob**, Sentinel |
| Observability | OpenTelemetry → Azure Monitor; Sentinel SIEM |
| Resilience | Polly; multi-region active-passive (RTO<1h / RPO<1min); 99.99% HA target |
| Activation | MCP server, SQL, REST API, SDK |

## Intellectual property

- **Trade secrets:** the signed-audit-ledger + sovereign-tenant architecture and control-mapping framework.
- **Patents (provisional):** immutable, signed **audit ledger of AI/data-context access** with destruction attestation.
- **Trademarks:** "Doxa," "The Trust Layer for Enterprise AI."
- **Open source (defensive):** connectors / MCP adapters, to drive adoption while keeping the trust spine proprietary.

## Roadmap

```mermaid
timeline
  title Doxa product roadmap (raise-funded)
  Pre-seed (0-6 mo) : MVP - catalog + lineage + policy : Immutable audit ledger : MCP activation : 3-4 design partners
  6-12 mo : AI Governance depth : Data quality + certification : SOC 2 readiness : First paid logos
  12-18 mo : Sovereign / air-gapped edition : Reverse metadata : Seed raise
  Post-seed : FedRAMP / StateRAMP path : Marketplace + connectors : Scale GTM
```

Roadmap reuses Atlan's phase skeleton, re-sequenced for a two-founder pre-seed reality (see
[09](09-financial-plan.md) for the milestone-to-funding mapping).
