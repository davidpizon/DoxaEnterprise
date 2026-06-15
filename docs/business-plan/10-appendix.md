# 10 — Appendix

← [Index](00-README.md)

## A. Glossary

| Term | Meaning |
|---|---|
| **Active metadata** | Metadata that updates from real-time events and triggers actions, vs. periodic crawls. |
| **Lineage** | Asset + process graph showing where data comes from and goes (incl. column-level). |
| **WORM** | Write-Once-Read-Many immutable storage (here, the audit ledger). |
| **Cryptographic shredding** | Rendering data unrecoverable by destroying its encryption key. |
| **CMK** | Customer-Managed Key (encryption keys the customer controls). |
| **Persona / Purpose** | Access-control model: *who* (persona) may use data for *what* (purpose). |
| **MCP** | Model Context Protocol — how AI clients consume tools/context. |
| **ARR / MRR** | Annual / Monthly Recurring Revenue. |
| **CAC / LTV** | Customer Acquisition Cost / Lifetime Value. |
| **NRR** | Net Revenue Retention (expansion − churn on the installed base). |
| **Rule of 40** | Growth% + profit-margin% ≥ 40 is healthy for SaaS. |
| **ATO / FedRAMP** | US government Authorization To Operate / cloud security authorization program. |

## B. Compliance control-mapping exhibit *(credibility — source: [`../spec`](../spec/doxa-enterprise-architecture-compliance-spec.md))*

| Framework / control | Doxa implementation |
|---|---|
| **SOC 2 CC6.0** (access control) | WAF + Entra ID conditional access; persona/purpose policy |
| **SOC 2 CC7.0** (system operations) | Defender for Cloud, continuous vuln scanning, Sentinel |
| **HIPAA §164.312(a)** (access control) | Unique user ID, short-lived tokens (Keycloak/Entra) |
| **HIPAA §164.312(b)** (audit controls) | Centralized logs → immutable WORM audit ledger |
| **HIPAA §164.312(c)** (integrity) | Blob integrity monitoring; SHA-256 chained signing |
| **HIPAA §164.312(e)** (transmission security) | TLS 1.3 everywhere; legacy handshakes dropped at edge |
| **NIST 800-53 AU** (audit & accountability) | Decoupled async audit pipeline; SHA-256 signed; WORM + legal hold |
| Encryption at rest | AES-256, customer-managed keys, 90-day rotation |
| Tenant isolation | Database-per-tenant (prod); RLS (dev) |
| Resilience | Active-passive multi-region; RTO<1h / RPO<1min; 99.99% HA |

## C. Differentiation matrix

| Capability | Doxa | Atlan | Collibra | OneTrust | Credo AI |
|---|---|---|---|---|---|
| Catalog / lineage | ✅ (building) | ✅ | ✅ | ⚪ | ⚪ |
| AI context activation (MCP/SQL/API) | ✅ | ✅ | ⚪ | ⚪ | ⚪ |
| AI governance (agent/model access) | ✅ | ⚪ | ⚪ | ✅ | ✅ |
| **Immutable signed audit ledger** | ✅ **unique** | ⚪ | ⚪ | ⚪ | ⚪ |
| **Data sovereignty / air-gap** | ✅ **unique** | ⚪ (in-VPC crawler) | ⚪ | ⚪ | ⚪ |
| **DB-per-tenant + crypto-shred offboarding** | ✅ **unique** | ⚪ | ⚪ | ⚪ | ⚪ |
| Control-mapped by construction | ✅ | partial | partial | ✅ | partial |

✅ = strong · ⚪ = weak/absent. Doxa aims for parity on table-stakes governance and **uniqueness on audit + sovereignty**.

## D. Competitor profiles (brief)

- **Atlan** — the model; catalog/active-metadata/"Context for AI", SaaS-first; weaker on immutable audit & sovereignty.
- **Collibra** — enterprise governance incumbent; broad, less AI-native, not audit-immutable.
- **Alation** — catalog/discovery strength; similar audit/sovereignty gaps.
- **Microsoft Purview** — Azure-native governance; competitor *and* interop partner (Doxa layers on top).
- **Databricks Unity Catalog / Snowflake Horizon** — platform-native governance; interop/competitive.
- **OneTrust** — GRC/privacy/AI-governance; compliance-strong, not a catalog/lineage platform.
- **Credo AI / Holistic AI** — AI-governance pure-plays; policy/risk, no data-catalog/immutable-audit spine.
- **Drata / Vanta-adjacent** — compliance automation; adjacent, certification-focused.

## E. Risk register

| Risk | Likelihood / Impact | Mitigation |
|---|---|---|
| Pre-product execution | High / High | Tight MVP on existing scaffold; design-partner validation |
| Incumbents add AI-governance/audit | Med / High | Move fast on audit+sovereignty wedge; patent the ledger |
| Long regulated/gov sales cycles | High / Med | Land private-sector first; integrators for gov; Assessment→Sprint shortens time-to-value |
| Certification cost/time (SOC 2→FedRAMP) | Med / Med | Phased; budgeted line in [90](90-financial-model.md); start readiness pre-seed |
| Founder / key-person & hiring | Med / High | Equity + advisor bench; staged hiring |
| Funding / runway | Med / High | Conservative burn; clear next-raise milestones; scenarios |
| Azure single-cloud concentration | Low / Med | Architecture portable in principle; multi-cloud later |
| AI/data-regulation flux | Med / Low (tailwind) | Control-mapping framework absorbs new frameworks |

## F. Assumptions log

All modeling assumptions and their rationale are in [90 §A](90-financial-model.md#a-key-assumptions). Headlines:
$1.25M SAFE raise; blended ACV $40K→$120K; GM 60%→85%; NRR 105%→125%; gross churn 10%→6%; runway ~18 months.
**Every financial figure is illustrative, not a forecast of actual results.**

## G. References

- [`../spec/doxa-enterprise-architecture-compliance-spec.md`](../spec/doxa-enterprise-architecture-compliance-spec.md) — compliance & security architecture.
- [`../plan/multi-tenant-cicd-data-isolation-architecture-plan.md`](../plan/multi-tenant-cicd-data-isolation-architecture-plan.md) — DB-per-tenant, CMK, CI/CD.
- [`../plan/enterprise-governance-security-operations-plan.md`](../plan/enterprise-governance-security-operations-plan.md) — crypto-shred, Sentinel, anomaly detection.
- [`../plan/automated-incident-response-playbook.md`](../plan/automated-incident-response-playbook.md) — SOAR incident response.
- [`../examples/atlan-context-platform-clone-spec.md`](../examples/atlan-context-platform-clone-spec.md) — the product model adapted here.
- [Atlan](https://atlan.com/) — positioning reference.
- Market category sizes: `[SOURCE: analyst reports — fill at diligence]`.

## H. Appendix exhibits to add before fundraising

Product wireframes/screenshots (post-MVP); design-partner letters of intent; detailed cap table; the live
`model.xlsx`; signed compliance attestations (once obtained).
