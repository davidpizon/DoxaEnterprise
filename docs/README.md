# Doxa Enterprise — Documentation Index

This folder holds the architecture, security, and reference documentation for the **Doxa Enterprise**
platform (a multi-tenant, .NET Aspire–based SaaS for regulated industries). The source solution lives
in [`../src`](../src).

## Architecture, Security & Compliance

These describe how Doxa itself is built, secured, deployed, and kept compliant (SOC 2, HIPAA, NIST).

| Document | What it covers |
| --- | --- |
| [doxa-enterprise-architecture-compliance-spec.md](doxa-enterprise-architecture-compliance-spec.md) | System architecture & compliance spec on Azure: HA/DR, zero-trust security, SOC 2 / HIPAA control mappings, immutable NIST audit pipeline (with Mermaid diagram). |
| [azure-deployment-security-implementation-plan.md](azure-deployment-security-implementation-plan.md) | Implementation plan: Bicep for container-level WORM immutable audit storage, plus Microsoft Purview DLP rules for multi-tenant PHI/PII protection. |
| [multi-tenant-cicd-data-isolation-architecture-plan.md](multi-tenant-cicd-data-isolation-architecture-plan.md) | GitHub Actions OIDC CI/CD (lint → validate → what-if → staged deploy) and the database-per-tenant Elastic Pool sharding model with database-level TDE CMK. |
| [enterprise-governance-security-operations-plan.md](enterprise-governance-security-operations-plan.md) | Security operations runbooks: certified tenant offboarding & cryptographic shredding (SQL/PowerShell), plus a Bicep pipeline streaming telemetry to Microsoft Sentinel with a cross-tenant-anomaly KQL hunting rule. |

## AI Agent & Data Patterns

Cross-cutting guidance and standards for the agent/MCP-oriented parts of the platform.

| Document | What it covers |
| --- | --- |
| [automated-data-architecture-for-ai-agents.md](automated-data-architecture-for-ai-agents.md) | Best practices for autonomous agent data stores: idempotent ingestion, tool specs, hybrid search, dynamic memory loops, runtime safety. |
| [agent-flavored-markdown-specification.md](agent-flavored-markdown-specification.md) | Reference for the WSO2 Agent-Flavored Markdown (AFM) spec, plus an **Applying AFM to Doxa** section with ready-to-use `.afm.md` agent examples. |

## Product Recreation Prompts (Research)

Exhaustive build prompts reverse-engineered from comparable SaaS products — reference material for
shaping Doxa's own agent-governance and MDM capabilities.

| Document | Source product |
| --- | --- |
| [backplanes-spotlight-recreation-prompt.md](backplanes-spotlight-recreation-prompt.md) | Backplanes Spotlight — AI agent session observability & governance. |
| [mintmcp-gateway-recreation-prompt.md](mintmcp-gateway-recreation-prompt.md) | MintMCP — enterprise MCP gateway & agent-governance platform. |
| [profisee-mcp-mdm-recreation-prompt.md](profisee-mcp-mdm-recreation-prompt.md) | Profisee — MDM platform with an MCP server exposing golden records to AI. |
