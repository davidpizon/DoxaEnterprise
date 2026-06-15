# 05 — Business Model

← [Index](00-README.md)

## Revenue streams

1. **Subscription (primary):** annual SaaS contracts across three tiers (below). Multi-year for regulated/gov.
2. **Governance Sprint services (early):** paid 4–6 week engagements that land design partners and seed
   subscriptions (see [06-go-to-market.md](06-go-to-market.md)). High early-stage share, declining over time.
3. **Premium support & success:** named CSM, priority SLAs, dedicated environments (bundled into upper tiers).
4. **Future:** connector/marketplace and verified-content lines (post-Series A).

*Revenue mix over time (subscription vs services) — source: [90 §B](90-financial-model.md#b-revenue-build).*

## Subscription tiers

Tiers ascend on **isolation → sovereignty → audit guarantees** — exactly where Doxa's moat is strongest and
where regulated/government buyers pay premiums.

| | **Essentials** (Governed) | **Enterprise** (Compliance-grade) | **Sovereign** (Air-gapped / Gov) |
|---|---|---|---|
| **Buyer** | Security-conscious mid-market; single-BU pilot | Regulated enterprise, multi-BU | Government / defense / highest-sensitivity |
| **Tenancy** | Multi-tenant, row-level isolation (RLS) | **DB-per-tenant**, CMK encryption | Single-tenant / **air-gapped / in-region sovereign** |
| **Core platform** | Catalog, lineage, glossary, classification | + Policy engine, persona/purpose, **AI Governance** | + Full feature set on customer/gov cloud |
| **AI activation** | MCP/API/SDK (capped endpoints) | Expanded MCP/SQL/API, more agents | Unlimited, dedicated activation |
| **Immutable audit** | Standard audit log | **WORM signed ledger**, configurable retention | WORM + legal hold + extended sovereign retention |
| **Offboarding** | Standard delete | **Cryptographic-shred** certified | Crypto-shred + destruction attestation |
| **Compliance** | SOC 2 (once certified) | SOC 2 + HIPAA BAA | + FedRAMP/StateRAMP path, NIST 800-53 |
| **DR / SLA** | Standard | 99.99%, multi-region, RTO<1h/RPO<1min | Sovereign DR per contract |
| **Support** | Standard | Priority + named CSM | Dedicated + Governance Sprint included |
| **Illustrative ACV** | **~$25K–60K** | **~$75K–200K** | **~$250K–750K+** |

*ACV ranges are pre-seed assumptions — source: [90 §A/§G](90-financial-model.md#a-key-assumptions).*

### Why this structure
- It monetizes the moat directly: the isolation→sovereignty→air-gap spectrum is precisely what regulated and
  government buyers pay premiums for — and precisely what Doxa's infrastructure already supports.
- **Sovereign** is the standout vs. Atlan/Collibra (SaaS-first, at most in-VPC) — a genuine public-sector/defense wedge.

## Pricing strategy

- **Value-based + platform-tier**, with capacity dimensions: connected sources, governed assets,
  AI agents / MCP endpoints, audit-retention duration, deployment model.
- **Enterprise motion:** published price guidance for Essentials; "contact sales" for Enterprise/Sovereign.
- **Land-and-expand:** start with one BU/use case, expand across business units and agencies (drives NRR).

## Customer acquisition & retention

- **Acquisition:** founder-led → design partners → referenceable logos → integrator-led public sector
  (full funnel in [06-go-to-market.md](06-go-to-market.md)).
- **Retention:** the immutable audit ledger becomes the system of record for AI access — rip-out cost is high
  once embedded; certification and policy history compound switching costs. Targets: gross logo churn
  10%→6%, **NRR 105%→125%** *(source: [90 §G](90-financial-model.md#g-saas-metrics-dashboard))*.

## Unit economics *(illustrative — source: [90](90-financial-model.md))*

| Metric | Early (Y1–Y2) | Mature (Y4–Y5) |
|---|---|---|
| Blended new ACV | $40K–55K | $100K–120K |
| Gross margin | 60–68% | 82–85% |
| New-logo CAC | $35K–45K | $50K–55K |
| CAC payback | ~24–30 months | ~13–15 months |
| LTV : CAC | <1 → ~1.5 | ~3.3 → ~4.0 |

> Early unit economics are honestly negative — a pre-product, long-cycle, enterprise/government motion. The
> model's thesis: **regulated switching costs + NRR** drive strong long-run economics once the base installs.

## Why the model fits regulated / government buyers

Long, multi-year contracts; expansion across BUs/agencies; high NRR; procurement friction that becomes
defensibility once Doxa is the embedded system of record for AI access.
