# Recreation Prompt — "Gateway": An Enterprise-Grade, Multi-Client MCP Gateway & Agent-Governance SaaS

> **How to use this file.** This is a single, self-contained build prompt. Hand it to a capable
> coding agent (or engineering team) to recreate a product equivalent to **MintMCP**
> (https://www.mintmcp.com) — an enterprise **MCP (Model Context Protocol) Gateway** and
> agent-governance platform that sits between AI agents (Claude, ChatGPT, Cursor, Gemini, Copilot)
> and an organization's internal tools/data, adding authentication, tool-level access control,
> credential brokering, audit, and policy at scale. The prompt is deliberately exhaustive: product
> vision, personas, control-plane/data-plane architecture, every concept (Virtual MCP Bundles, Agent
> Bundles, hosted connectors, gateway middleware), the full security model, multi-tenancy, data
> model, APIs, deployment, and acceptance criteria.
>
> Reference product studied (June 2026): MintMCP, and its blog post *"Best MCP Gateways for SaaS
> Companies 2026"* (https://www.mintmcp.com/blog/gateway-saas-with-mcp). This is an independent
> recreation spec, not affiliated with MintMCP.

---

## 0. Role & Objective

You are a principal platform architect. Build **Gateway** — a production-ready, cloud-scale,
**multi-tenant (multi-client) SaaS** that lets enterprises safely connect AI agents to internal
tools and data through the Model Context Protocol. The platform is **data-permissions-first**:
identity, access control, and audit come first; agents are enabled on top of that foundation.

Positioning to preserve: *"Agent governance that lets your team do more with AI, securely."*
It is **compliance-first enterprise infrastructure**.

The system has two product modules and three architectural planes:

- **Module A — MCP Gateway:** managed hosting + a governed proxy that wraps many upstream MCP
  servers behind SSO-fronted **remote MCP endpoints**.
- **Module B — Agent Monitor:** real-time visibility and guardrails over what coding agents do
  (file reads, command execution, MCP tool calls), with rule-based detection and blocking.
- **Planes:** a **Control Plane** (admin UI + APIs for identity, bundles, connectors, policy,
  audit), a **Data Plane** (the high-throughput MCP proxy/runtime), and an **Identity Plane**
  (SSO/SCIM/OAuth brokering + credential vault).

---

## 1. Background — What an MCP Gateway Is and Why It Exists

An **MCP Gateway** is middleware between AI agents and internal tools/data sources. It solves three
production challenges: **tool organization, protocol translation, and security control.**

Motivating pain points (cite as design rationale):
- "86% of enterprises require tech stack upgrades to properly deploy AI agents."
- "42% require eight or more data sources per agent deployment."
- "62% express serious concern about security and compliance risks."
- Only "18% have enterprise-wide AI governance councils despite widespread generative AI usage."

**MCP primer the build must honor:**
- An **MCP server** exposes **tools**, **resources**, and **prompts**. An **MCP client** (the AI
  agent's host) connects to servers and invokes them.
- Transports: **stdio** (local subprocess) and **streamable HTTP** (remote); **SSE** is the legacy
  remote transport and must still be supported. The gateway must speak all of these.
- The gateway is **dual-role**: to AI clients it *is* an MCP server (an aggregating remote endpoint);
  to upstream tools it is an MCP client.
- Remote MCP auth follows **OAuth 2.1** with Protected Resource Metadata and Dynamic Client
  Registration; the gateway brokers this so end clients authenticate via the org's SSO.

---

## 2. Target Audiences & Jobs-to-be-Done

| Persona | Needs | What Gateway gives them |
| --- | --- | --- |
| **Platform / IT admin** | Govern which agents reach which tools | SSO/SCIM, RBAC, bundles, tool allowlists, credential vault |
| **Security / CISO / Compliance** | Prove control, prevent leakage | Per-bundle audit trails, DLP, PII/secret scanning, SOC2/HIPAA evidence |
| **Engineering teams (consumers)** | One-click access to approved tools | Curated bundle endpoint URL; no manual credential setup |
| **Data / analytics teams** | Governed access to warehouses | Hosted connectors (Snowflake, BigQuery, Databricks) with scoped tools |
| **AI platform owners** | Run internal agents safely | Agent Bundles with M2M identity + "act as agent" |

---

## 3. High-Level Architecture

```
   AI Clients (Claude / ChatGPT / Cursor / Gemini / Copilot / Windsurf)
                              │  remote MCP endpoint (HTTPS, OAuth 2.1, SSO-fronted)
                              ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│  DATA PLANE — MCP Proxy / Runtime (stateless, horizontally scaled)             │
│  • terminates remote MCP (streamable HTTP + SSE)                               │
│  • resolves caller → org/user/agent → Virtual MCP Bundle                       │
│  • enforces tool allowlist + rule-based policy + JS middleware (sandboxed)     │
│  • fans out to upstream MCP servers (stdio runtime / hosted connectors / HTTP) │
│  • injects brokered credentials; runs DLP + PII/secret scanning                │
│  • emits audit + telemetry for every tool call                                 │
└───────────────┬───────────────────────────────────────────────┬───────────────┘
                │                                                 │
                ▼                                                 ▼
   ┌─────────────────────────┐                      ┌──────────────────────────────┐
   │ IDENTITY PLANE          │                      │ CONTROL PLANE (Admin UI + API)│
   │ • SSO (SAML/OIDC)       │                      │ • orgs, users, agents, roles  │
   │ • SCIM directory sync   │                      │ • Virtual MCP / Agent Bundles │
   │ • OAuth brokering        │                      │ • connector catalog + config  │
   │ • credential vault (KMS)│                      │ • policy, middleware, DLP      │
   └─────────────────────────┘                      │ • audit log + observability   │
                                                     └──────────────────────────────┘
   ┌──────────────────────────────────────────────────────────────────────────────┐
   │  MANAGED MCP RUNTIME — hosts STDIO-based MCP servers (per-tenant isolation),   │
   │  lifecycle mgmt, autoscale, health; + Hosted Connectors (Snowflake, Gmail, …)  │
   └──────────────────────────────────────────────────────────────────────────────┘

   AGENT MONITOR (Module B): agent-side capture of file reads / commands / tool calls
   → streamed to Control Plane → rule-based detection + inline blocking + dashboards
```

---

## 4. Module A — MCP Gateway (core)

### 4.1 Managed MCP runtime (STDIO hosting)
- **Deploy STDIO-based MCPs with hosted runtime, authentication, and lifecycle management.**
- Run each upstream MCP server in an **isolated, per-tenant sandbox** (container/microVM); manage
  start/stop/health/restart, version pinning, autoscaling, and resource quotas.
- Pre-configure credentials so consumers never set up secrets manually.

### 4.2 Connector catalog & hosted connectors
- A **centralized server catalog** enabling **one-click connections** with **pre-configured
  credentials**.
- Ship **hosted connectors** that run inside the platform's infrastructure. Target a large library
  ("100+" hosted, "1,000+" pre-built), including at minimum: **Snowflake, BigQuery, Databricks,
  Elasticsearch, Gmail, Outlook, Google Calendar, Outlook Calendar, Google Drive, SharePoint,
  Confluence, Slack, Teams, Salesforce, Notion, Linear, PostgreSQL/enterprise databases, custom
  internal APIs.**
- Each connector declares: auth type (OAuth/API key/M2M), required scopes, exposed tools, and a
  default policy.

### 4.3 Virtual MCP Bundles (flagship concept)
**Create per-use-case endpoints with SCIM-driven membership, curated tool lists, and access policy.**
A bundle is a virtual MCP server composed from a curated subset of tools across one or more upstream
servers, published as a single SSO-fronted remote endpoint.

- **SCIM-driven membership** that auto-syncs with the identity provider (add/remove users by IdP
  group; no manual per-server config).
- **Curated tool lists per team or role** — expose only the minimum required tools (prevents "tool
  sprawl").
- **Isolated audit trails per bundle** — simplifies compliance reporting.
- **Fine-grained, rule-based access policy** at the tool and parameter level.
- Each bundle has a stable endpoint URL that consumers paste into their AI client.

### 4.4 Agent Bundles
**Govern agent identities with M2M auth, scoped tools, and an "act as agent" flow.**
- Per-agent identity (distinct from human users); **machine-to-machine (M2M) authentication**.
- Scoped tool access independent of any human's permissions.
- **"Act as agent"** flow so a human can operate within an agent's identity/permission envelope for
  testing and supervised runs.

### 4.5 Gateway middleware & policy
- **JavaScript Gateway Middleware** executed in a **sandboxed environment** for inline policy:
  inspect/modify/deny requests and responses, redact fields, enforce business rules.
- **Tool-level allowlisting** and **rule-based policy** evaluated on every call.
- **DLP integration** and **PII/secret scanning** on tool inputs/outputs (mask credit cards, SSNs,
  emails, phone numbers; integrate a Presidio-style detector).
- **OAuth brokering + credential management** for both MCP servers and connectors — secrets live in
  a KMS-backed vault; the gateway injects them at call time; clients never see them.

### 4.6 LLM Proxy
- An **LLM Proxy** that routes/observes model traffic alongside MCP traffic, enabling unified usage
  metering, model-access policy, and traces across both the model and tool layers.

### 4.7 Complete audit trails & observability
- **Centralize audit logs and observability for MCP activity, tool access, and policy enforcement.**
- Every tool call records: timestamp, org, user/agent identity, bundle, upstream server+tool,
  parameters (post-redaction), decision (allow/deny/modified), latency, result status, and policy/
  middleware that fired.
- **OpenTelemetry export** of traces/metrics/logs to customer SIEM/observability stacks.

---

## 5. Module B — Agent Monitor

- Provides **real-time visibility into coding-agent tool calls with security guardrails.**
- Agent-side capture of **file reads, command execution, and MCP tool calls.**
- **Rule-based detection and blocking** — define rules that flag or block dangerous actions inline.
- Governs across multiple AI platforms (pairs with the Gateway for end-to-end coverage).
- Dashboards for what each agent session did; alerts on policy violations.

---

## 6. Security, Identity & Compliance (first-class, not bolted on)

### 6.1 Identity & access
- **SSO:** SAML and OIDC (Okta, Azure AD/Entra, Google Workspace).
- **SCIM directory sync** for users and groups; **IdP group → bundle membership** mapping.
- **RBAC** across the entire organization; least-privilege defaults.
- **OAuth brokering** with a centralized **credential vault** (envelope encryption, KMS-managed
  keys, per-org isolation, rotation).
- **M2M auth** for agents.

### 6.2 Data protection
- TLS everywhere; encryption at rest with per-tenant key separation.
- DLP + PII/secret scanning inline; redaction before audit storage and before responses leave the
  boundary.
- Tenant isolation enforced at the data-access layer (row-level security or equivalent) and at the
  runtime layer (sandbox per tenant).

### 6.3 Compliance posture
- **SOC 2 Type II audited** infrastructure (audit logging, access control, change management,
  encryption).
- **HIPAA-compliant** with **Business Associate Agreement (BAA)** available.
- **GDPR** alignment; design for **EU AI Act** obligations (note penalties up to €35M or 7% of
  worldwide annual turnover for prohibited-practice violations).

---

## 7. Multi-Tenancy & Commercial Model

- First-class **Organization (client/tenant)** entity; everything is tenant-scoped and isolated.
- Identity hierarchy: **Org → Team → (User | Agent) → Bundle membership → Tool grants.**
- Designed to scale across team-size brackets: **1–100, 101–1,000, 1,001–9,999, 10,000+** users.
- **Per-user licensing with scalable platform fees** ("custom pricing based on team size and
  needs"). Implement metered usage internally (tool calls, connector minutes, LLM tokens).
- **Enterprise add-ons:** SSO/SAML, SCIM directory sync, OpenTelemetry export, dedicated success
  management, white-glove deployment, managed agents (early access).

---

## 8. Deployment & Operations

- **Managed SaaS-first** in **US and EU** regions; **VPC / self-hosted** deployment available on
  request (and on-premise on contact).
- Containerized control plane + data plane; Kubernetes-native; IaC (Terraform/Bicep).
- Horizontal autoscale of the stateless proxy; per-tenant sandbox pools for the STDIO runtime.
- Full self-observability (the platform monitors itself), blue/green or rolling deploys, regional
  data residency.

---

## 9. Supported AI Clients (must connect out-of-the-box)

Claude, ChatGPT, Cursor, Gemini, Copilot, Windsurf. For each, document how to register the bundle's
remote MCP endpoint URL and complete the SSO/OAuth handshake.

---

## 10. Recommended Tech Stack (adapt as needed)

- **Data-plane proxy/runtime:** a high-concurrency service (Go, Rust, or .NET) implementing the MCP
  server+client roles, streamable HTTP + SSE + stdio bridging.
- **STDIO runtime isolation:** containers/microVMs (gVisor/Firecracker-style) orchestrated on
  Kubernetes; per-tenant namespaces and quotas.
- **Control plane:** stateless API services + admin web app (React/Next or Blazor); Postgres (RLS
  for tenancy); object storage for large audit payloads; Redis for caching/rate limits.
- **Identity:** SAML/OIDC + SCIM server; OAuth 2.1 authorization-server/broker; KMS-backed secret
  vault (e.g., cloud KMS + envelope encryption, or HashiCorp Vault).
- **Policy/middleware:** sandboxed JS runtime (e.g., V8 isolates / QuickJS / Workers-style) for
  gateway middleware; a rules engine for allowlist/deny.
- **DLP/PII:** Presidio-style detector integrated inline.
- **Observability:** OpenTelemetry pipeline; export to customer SIEM.

---

## 11. Core Data Model (minimum)

`Organization`, `Team`, `User`, `AgentIdentity`, `Role`, `IdPConnection` (SAML/OIDC + SCIM),
`Connector` (catalog entry), `ConnectorInstance` (configured + credentialed), `McpServerRuntime`
(hosted STDIO instance), `VirtualBundle` (+ `BundleMember`, `BundleToolGrant`, `BundlePolicy`),
`AgentBundle`, `CredentialSecret` (vault ref), `MiddlewareScript`, `PolicyRule`, `AuditEvent`,
`UsageMeter`, `AgentMonitorEvent`.

---

## 12. APIs & Endpoints (representative)

- **Remote MCP endpoints (data plane):** `https://<region>.gateway/<org>/bundles/<bundle>/mcp`
  (streamable HTTP) + SSE variant; OAuth 2.1 protected, SSO-fronted.
- **Admin API (control plane):** CRUD for orgs/teams/users/agents/roles; connectors + instances;
  bundles (membership, tool grants, policy); middleware scripts; credential vault refs; audit query;
  usage/metering; Agent Monitor rules + events.
- **SCIM 2.0** `/scim/v2/Users` and `/Groups`. **SAML/OIDC** ACS + discovery endpoints.
- **Webhooks/streaming** for audit + monitor events; **OTel exporter** config.

---

## 13. Required Deliverables

1. **Data-plane MCP proxy** speaking stdio + streamable HTTP + SSE in both client and server roles,
   with per-call allowlist + policy + sandboxed JS middleware + credential injection + DLP/PII scan
   + audit emission.
2. **Managed STDIO runtime** with per-tenant sandbox isolation and full lifecycle management.
3. **Connector catalog** + a starter set of hosted connectors (Snowflake, BigQuery, Gmail, Slack,
   Salesforce, Notion, Linear, Postgres) with one-click, pre-credentialed setup.
4. **Virtual MCP Bundles** and **Agent Bundles** (SCIM membership, curated tools, M2M, "act as
   agent").
5. **Identity plane:** SSO (SAML/OIDC), SCIM sync, IdP-group→bundle mapping, OAuth brokering,
   KMS-backed credential vault.
6. **Audit + observability:** complete per-call audit trails (per-bundle isolation), OTel export.
7. **LLM Proxy** with usage metering and model-access policy.
8. **Agent Monitor** with capture, rule-based detection/blocking, and dashboards.
9. **Admin web app** + **Admin API** + **remote MCP endpoints**; multi-tenant throughout.
10. **Compliance surfaces:** trust center, audit-evidence export, BAA workflow, data-residency
    (US/EU) and VPC/self-hosted deployment artifacts.
11. **Docs:** client connection guides (Claude/ChatGPT/Cursor/Gemini/Copilot/Windsurf), connector
    setup, bundle/policy authoring, security/architecture whitepaper, API/SCIM reference.

---

## 14. Acceptance Criteria (definition of done)

- [ ] An AI client connects to a bundle's remote MCP endpoint, completes SSO/OAuth, and sees **only**
      the bundle's curated tools — no upstream credentials ever reach the client.
- [ ] Adding a user to an IdP group automatically grants the mapped bundle (SCIM), and removing them
      revokes it — with no manual per-server config.
- [ ] Every tool call is allow/deny/modify-evaluated by tool allowlist + rule policy + sandboxed JS
      middleware, with DLP/PII redaction, and produces an isolated, queryable audit event.
- [ ] STDIO-based MCP servers run in per-tenant isolation with managed lifecycle; one tenant cannot
      observe or reach another's runtime or secrets.
- [ ] Agent Bundles authenticate via M2M, carry scoped tools independent of human permissions, and
      support an "act as agent" flow.
- [ ] LLM Proxy meters usage and enforces model-access policy; OTel export delivers traces/metrics/
      logs to an external collector.
- [ ] Agent Monitor captures file reads/commands/MCP tool calls and can block on rule match in real
      time.
- [ ] Strict tenant isolation across data, runtime, and credentials; SOC 2-style audit logging,
      encryption at rest/in transit, and US/EU residency are demonstrable; VPC/self-hosted artifacts
      build and deploy.

---

## Sources

- [MintMCP — home](https://www.mintmcp.com/)
- [Best MCP Gateways for SaaS Companies 2026 (source article)](https://www.mintmcp.com/blog/gateway-saas-with-mcp)
- [MintMCP — pricing](https://www.mintmcp.com/pricing)
- [MintMCP — docs](https://www.mintmcp.com/docs)
- [Best MCP Gateways for HIPAA Compliance 2026](https://www.mintmcp.com/blog/mcp-gateways-hipaa-compliance)
- [Best MCP Gateways for Enterprise Engineering Teams 2026](https://www.mintmcp.com/blog/gateways-enterprise-engineering-with-mcp)
- [MintMCP vs TrueFoundry — Enterprise MCP Gateway Comparison 2026](https://www.mintmcp.com/blog/mintmcp-vs-truefoundry)
