# Recreation Prompt — "Spotlight": An Enterprise-Grade, Multi-Client AI Agent Session Observability SaaS

> **How to use this file.** This is a single, self-contained build prompt. Hand it to a capable
> coding agent (or engineering team) to recreate a product equivalent to **Backplanes Spotlight**
> (https://www.backplanes.com) — a privacy-first observability and governance platform that reads
> AI coding-agent sessions (Claude Code, Codex, and similar) and produces session-level and
> org-level reports across Security, Engineering, and Spend, plus an MCP/external-access inventory
> and governance workflow. The prompt is deliberately exhaustive: product vision, architecture,
> data model, every feature, security model, multi-tenancy, and acceptance criteria.
>
> Reference product studied (June 2026): Backplanes Spotlight. Tagline: *"See what your agent
> actually did."* This is an independent recreation spec, not affiliated with Backplanes.

---

## 0. Role & Objective

You are a principal full-stack architect. Build **Spotlight** — a production-ready, cloud-scale,
**multi-tenant (multi-client) SaaS** that gives engineering organizations complete visibility and
governance over what their AI coding agents actually do during each session, while guaranteeing that
sensitive code, secrets, and PII never leave the customer's machine unredacted.

The system has three deployable surfaces:

1. **Collector CLI** — a one-command-install local agent that reads coding-agent session logs,
   redacts secrets/PII **locally**, and ships sanitized telemetry to the cloud.
2. **Ingestion & Analysis Backend** — a multi-tenant API + pipeline that re-scrubs, encrypts,
   stores, analyzes (with an LLM), and serves reports.
3. **Web Dashboard** — a role-aware web app where engineers, managers, CFOs, and CISOs consume
   session reports, org reports, and the MCP/external-access governance console.

---

## 1. Product Vision & Positioning

- **One-liner:** "See what your agent actually did."
- **Problem:** AI coding agents (Claude Code, Codex) run autonomously — editing files, running
  commands, calling external APIs and MCP servers — often while the human is away. Teams have no
  audit trail, no spend visibility, and no governance over what external resources agents reach.
- **Core promise:** *"Your agent ran for 47 minutes while you were in a meeting. Spotlight watches
  every move and gives you quick feedback on what's worth keeping, what to fix, and where to save
  time next run."*
- **Differentiators (must preserve):**
  - **Passive, zero-instrumentation capture** — reads existing session logs; no SDK changes to the
    agent.
  - **Local-first privacy** — redaction happens on the user's machine *before* any network egress.
  - **Free to start, no seat counting, no trial clock**; custom pricing only for org-wide rollout
    with attribution + volume controls. "The service is free; users are not the product. No selling
    data to advertisers or AI labs."

---

## 2. Primary Audiences & What Each Must Get

Build distinct, role-scoped report views. Each audience below is a first-class persona with its own
dashboard landing view and RBAC role.

| Audience | What they need | Key views/metrics |
| --- | --- | --- |
| **Engineers & builders** | Fast review of their own agent sessions; what to keep/fix/optimize | Per-session report; scope-drift flags; review recommendations; shareable patterns/Skills |
| **Engineering managers** | Team AI capacity & adoption | Sessions/calls per person & per repo; Skill adoption ranking; unreviewed-tool spread |
| **CFOs / Finance** | Spend, ROI, capacity by team and tool | Spend view: cost/ROI attribution by team, tool, and time |
| **CISOs / Security** | External access, data egress, policy enforcement | Security view: external domains/MCP inventory, off-allowlist alerts, sanction/block workflow, audit trail |

---

## 3. High-Level Architecture

```
┌─────────────────────────┐        TLS + per-field encryption        ┌──────────────────────────────┐
│  Collector CLI (local)  │  ───────────────────────────────────▶   │   Ingestion API (multi-tenant) │
│  • discover sessions    │     redacted, sanitized event stream     │   • authN/Z (org/session/event)│
│  • parse Claude/Codex    │                                          │   • server-side re-scrub       │
│  • LOCAL redaction       │                                          │   • per-field encryption       │
│    (gitleaks + PII pass) │                                          │   • event store + queue        │
│  • redactions.log        │                                          └───────────────┬──────────────┘
└─────────────────────────┘                                                          │
                                                                                       ▼
                                                              ┌────────────────────────────────────┐
                                                              │   Analysis Pipeline                  │
                                                              │   • LLM summarization (ZDR providers)│
                                                              │   • scope-drift detection            │
                                                              │   • external-access inventory build  │
                                                              │   • spend/cost attribution           │
                                                              └───────────────┬──────────────────────┘
                                                                              ▼
                              ┌───────────────────────────────────────────────────────────────┐
                              │  Web Dashboard (role-aware: Eng / EM / CFO / CISO)             │
                              │  Session reports · Org reports · MCP & External Access console │
                              └───────────────────────────────────────────────────────────────┘
```

---

## 4. Surface 1 — Collector CLI

### 4.1 Install & onboarding
- **One-command install:** `curl -fsSL https://<host>/install.sh | sh` (provide Windows/PowerShell
  and Homebrew equivalents). Installs a small binary/daemon to `~/.spotlight/`.
- After install, `spotlight login` performs device-code OAuth/OIDC against the backend and binds the
  machine to an **organization** and **user identity**.
- `spotlight watch` (or a background launch agent) tails new sessions automatically; reports "roll
  up automatically from session reports."

### 4.2 Session discovery & parsing
- Auto-discover and parse sessions from supported agents:
  - **Claude Code** session transcripts/logs.
  - **Codex** session logs.
  - Architect parsers as **pluggable adapters** so Cursor, Gemini CLI, and custom MCP harnesses can
    be added later. Clearly document supported vs. unsupported.
- From each session, extract **only agent-initiated activity**:
  - Files the agent touched.
  - Commands it ran.
  - Domains and APIs it reached.
  - MCP servers, tools, Skills, plugins, and subagents it loaded.
  - Timeline / duration / token + cost signals (for spend).
  - Scope: intended task vs. actual actions (for drift detection).

### 4.3 Hard privacy boundaries (MUST enforce)
**Collect:** files the agent touched, commands it ran, domains/APIs reached, MCP servers/Skills/
subagents loaded, within supported sessions only.

**Never collect:** other repos, other terminals, other apps; the user's filesystem at large, email,
browser, or calendar; sessions from unsupported tools; files the agent never accessed.

### 4.4 Local redaction (before any egress)
- **Pass 1 — secrets:** strip credentials using **vendored gitleaks rules** (API keys, tokens,
  private keys, connection strings, etc.).
- **Pass 2 — PII:** second redaction pass for personal data.
- Write every redaction to `~/.spotlight/redactions.log` so users can audit exactly what was removed.
- Only redacted, sanitized payloads cross the network. Document this as a non-negotiable invariant.

---

## 5. Surface 2 — Ingestion & Analysis Backend (multi-tenant)

### 5.1 Multi-tenancy & isolation
- First-class **Organization (client/tenant)** entity. Every record is tenant-scoped; enforce
  tenant isolation at the data-access layer (row-level security or equivalent).
- Identity hierarchy: **Organization → Team → User → Machine → Session → Event**.
- **Enterprise SSO:** OIDC/SAML (e.g., Okta, Entra ID, Google Workspace). SCIM provisioning for
  org-wide rollouts.
- **RBAC roles:** Owner/Admin, Engineering Manager, Finance (CFO), Security (CISO), Engineer (member).
  Each role maps to the report scopes in §2. Attribution & volume controls are admin-configurable.

### 5.2 Secure ingestion
- Accept the collector's sanitized event stream over **TLS**.
- **Re-scrub server-side** ("never trust the client alone") — run secret/PII detection again.
- **Per-field encryption at rest**, with encryption keys bound to **organization + session + event**
  identifiers (envelope encryption / KMS-backed).
- Idempotent ingest keyed by event id; back-pressure via a durable queue.

### 5.3 Analysis pipeline
- Summarize each session with an LLM to produce the human-readable report and review
  recommendations.
- **LLM data policy:** use **Zero-Data-Retention** provider configurations — "zero retention always
  on at the LLM layer," contractually ensuring providers keep nothing of analyzed content (model
  this for both Anthropic and OpenAI-style providers).
- Derive: scope-drift findings, security findings, Skill/tool adoption stats, and spend/cost
  attribution.
- Build/maintain the **external-access inventory** (see §7) from session telemetry, with a rolling
  **90-day observation window** and last-accessed timestamps.

### 5.4 Data lifecycle
- On account/session deletion, purge tenant data except where retention is legally required.
- No sale of data to advertisers or AI labs. Material policy changes require prior user notice.

---

## 6. Surface 3 — Reports

### 6.1 Session report (per agent run)
For a single session, render:
- Summary of what the agent did and how long it ran.
- Files touched, commands run, external domains/APIs reached, MCP servers/Skills/subagents loaded.
- **Scope-drift** callouts (where actions diverged from the stated task).
- **Review recommendations:** "what's worth keeping, what to fix, where to save time next run."
- Shareable patterns/Skills surfaced for reuse.
- Explicit framing: reports **complement, not replace** code review, testing, and CI.

### 6.2 Org report (rolls up sessions) — three views
- **Security view:** external access & data egress posture; off-allowlist alerts; policy state;
  audit trail — the answer to a CISO's "what are we talking to?"
- **Engineering view:** Skill/tool adoption ranked by sessions & calls; standardization candidates;
  detection of unreviewed tools spreading across teams; sessions/calls per person & repo.
- **Spend view:** cost, ROI, and capacity by team and by tool over time.

---

## 7. Flagship Feature — MCP & External Access

Recreate this as a dedicated console. It "provides comprehensive visibility into every external
dependency agents interact with — MCP servers, tools, Skills, plugins, and domains — auto-generated
from session telemetry without additional instrumentation."

### 7.1 Inventory & monitoring
- Catalog **all** external domains, MCP servers, tools, Skills, and plugins accessed by agents.
- Per-resource usage metrics: **sessions, calls, and which team members reached it**.
- **Last-accessed timestamps** and a **90-day observation window**.
- Present in a **filterable, sortable** table (by resource type, usage, owner, status, recency).

### 7.2 Security & governance workflow
- **Flag off-allowlist domains on first detection.**
- **One-click decision workflow: sanction · review · block.**
- Surface resources that require review or are already blocked.
- Maintain an immutable audit log of every governance decision (who, what, when, why).
- Allowlist/blocklist managed per organization; changes propagate to alerting.

### 7.3 Engineering insights
- Rank Skills by sessions and calls to reveal adoption trends.
- Enable standardizing frequently-used Skills into an **organizational registry**.
- Detect unreviewed tools spreading across teams.

---

## 8. Trust, Privacy & Compliance (build these as product surfaces, not afterthoughts)

- A public **Trust page** documenting: exactly what is and isn't collected (§4.3), local-then-server
  redaction, per-field encryption with org/session/event-bound keys, TLS in transit, and ZDR at the
  LLM layer.
- User-visible `redactions.log` and an in-app "what we collected from this session" view.
- Commitments: free service / users-not-the-product; no data sales; legally-bounded retention;
  prior notice on material changes.
- Design toward **SOC 2 Type II** readiness (audit logging, access controls, encryption, change
  management) even if certification is a later milestone.

---

## 9. Pricing & Plans

- **Free tier:** individual developers and teams — no seat counting, no trial period.
- **Enterprise (custom):** org-wide rollout with attribution, volume controls, SSO/SCIM, advanced
  governance, and admin reporting. Implement metered usage internally even though the base is free.

---

## 10. Recommended Tech Stack (adapt as needed)

- **Collector CLI:** Go or Rust single static binary (cross-platform: macOS, Linux, Windows);
  vendored gitleaks ruleset; local config + logs under `~/.spotlight/`.
- **Backend:** stateless API services (.NET / Node / Go); Postgres (with row-level security for
  tenancy) + object storage for raw sanitized payloads; durable queue (e.g., Kafka/SQS) for ingest;
  KMS-backed envelope encryption; Redis for caching/rate-limits.
- **Analysis:** worker service calling ZDR-configured LLM providers (Anthropic Claude, OpenAI).
- **Web:** SPA or SSR (React/Next or Blazor); OIDC/SAML SSO; role-aware routing.
- **Cloud/infra:** containerized services; IaC (Bicep/Terraform); horizontal scale; full
  observability (logs/metrics/traces) on the platform itself.
- **Auth:** OIDC + SAML, SCIM, device-code flow for the CLI.

---

## 11. Required Deliverables

1. **Collector CLI** with: install script(s), session discovery/parsing adapters (Claude Code +
   Codex), two-pass local redaction, `redactions.log`, device-code auth, and background watch mode.
2. **Ingestion API** (multi-tenant) with TLS ingest, server-side re-scrub, per-field encryption,
   idempotent queue-backed pipeline, and the org/team/user/machine/session/event data model.
3. **Analysis workers** producing session reports, scope-drift, security findings, spend attribution,
   and the external-access inventory (90-day window).
4. **Web dashboard** with role-aware Eng/EM/CFO/CISO landing views, the session report, the three org
   report views, and the **MCP & External Access** governance console (inventory + sanction/review/
   block + audit log).
5. **Enterprise controls:** SSO (OIDC/SAML), SCIM, RBAC, allowlist/blocklist, attribution & volume
   controls, tenant isolation.
6. **Trust surfaces:** public trust page, in-app data-collection transparency, deletion flow.
7. **Docs:** install guide, security/architecture whitepaper, API reference, admin guide.

---

## 12. Acceptance Criteria (definition of done)

- [ ] One-command install works on macOS/Linux/Windows; CLI binds a machine to an org via SSO.
- [ ] No unredacted secret or PII ever leaves the local machine; every redaction is logged locally
      and the server independently re-scrubs.
- [ ] Each supported session auto-produces a session report with files, commands, external access,
      MCP/Skills, scope-drift, and review recommendations — with **zero** agent instrumentation.
- [ ] Org reports render correct Security, Engineering, and Spend views, scoped by RBAC role.
- [ ] MCP & External Access console catalogs every external domain/MCP/tool/Skill/plugin with
      sessions, calls, who-reached-it, last-accessed, over a 90-day window; off-allowlist resources
      are flagged on first detection; sanction/review/block is one click and fully audited.
- [ ] Strict tenant isolation; no cross-organization data access is possible.
- [ ] LLM analysis runs under zero-data-retention provider configs.
- [ ] Free tier requires no seat counting or trial; enterprise adds SSO/SCIM, attribution, and
      volume controls.

---

## Sources

- [Backplanes Spotlight — home](https://www.backplanes.com/)
- [MCP & External Access feature](https://www.backplanes.com/features/mcp-external-access)
- [Trust / security page](https://www.backplanes.com/trust)
- [Spotlight by Backplanes — Product Hunt](https://www.producthunt.com/products/backplanes)
- [Spotlight by Backplanes — ChatGate overview](https://chatgate.ai/post/spotlight-by-backplanes)
- [Spotlight by Backplanes — completeaitraining](https://completeaitraining.com/ai-tools/spotlight-by-backplanes/)
