# Agent-Flavored Markdown (AFM) Specification — Reference

> **Source & attribution.** This is a project reference summary of the **Agent-Flavored Markdown
> (AFM)** specification by WSO2. The canonical, authoritative document lives at
> <https://wso2.github.io/agent-flavored-markdown/specification/>. AFM is an evolving draft —
> always defer to the upstream spec for normative detail.
>
> **Version captured:** `0.4.0` (draft).

---

## 1. Overview

AFM is a Markdown-based format that lets an AI agent be **defined once in text and deployed across
multiple platforms**. It favors a declarative approach over "complex, imperative code."

### Design goals

1. **No Code** — declarative, text-based definitions readable by developers and non-technical
   stakeholders alike.
2. **Portable** — the same agent definition is interpretable across diverse platforms.
3. **Unified** — works for both code-based and visual / low-code interfaces.
4. **Adaptable** — designed to evolve with AI technologies and requirements.

### Normative language

The spec uses RFC 2119 / 8174 terms: **MUST / MUST NOT**, **SHOULD / SHOULD NOT**, **MAY**, and
**SHALL** (equivalent to MUST).

---

## 2. File structure & format

- **Extension:** an agent definition file **MUST** use the `.afm.md` or `.afm` extension.
- Each file has two parts:
  1. **Front matter** — an OPTIONAL YAML block delimited by `---`, holding metadata and configuration.
  2. **Markdown body** — the agent directives, which **MUST** contain two headings:
     - `# Role` — the agent's purpose and responsibilities.
     - `# Instructions` — behavioral directives and operational guidelines.

### Minimal valid AFM file

The front matter is optional; only the body headings are required.

```markdown
# Role

You are a helpful assistant.

# Instructions

Provide clear and concise responses to user queries.
```

---

## 3. Front matter schema

### Agent details

| Field | Meaning |
| --- | --- |
| `spec_version` | AFM specification version (e.g., `"0.4.0"`); lets implementations validate compatibility. |
| `name` | Human-readable agent identifier. Implementations **SHALL** use it to display the agent in UIs. |
| `description` | Concise summary of agent functionality. |
| `version` | Agent semantic version (MAJOR.MINOR.PATCH). |
| `author` / `authors` | Creator attribution, format `Name <Email>`. |
| `provider` | Organization details (`name`, `url`). |
| `icon_url` | Visual representation URL. |
| `license` | Release licensing. |

### Model

The AI model powering the agent.

```yaml
model:
  name: string
  provider: string        # e.g. "openai", "anthropic"
  url: string
  authentication: object
```

### Interfaces

How the agent receives input and produces output. If `interfaces` is **not** defined, implementations
**MUST** assume a default interface of type `consolechat`.

```yaml
interfaces:
  - type: consolechat | webchat | webhook
    signature: object      # optional JSON Schema for input/output
    exposure: object       # optional HTTP configuration
    subscription: object   # webhook event subscriptions
```

- **consolechat** — command-line / terminal chat.
- **webchat** — browser-based conversational UI.
- **webhook** — webhook endpoint with subscription support. Adds `prompt` (template string with
  variable substitution), `subscription` (protocol, hub, topic, callback, secret, authentication),
  and `exposure` (HTTP path, default `/webhook`).

`signature` objects conform to **JSON Schema** (simple types or complex objects with validation).

### Tools (Model Context Protocol)

External tools are defined via MCP servers.

```yaml
# HTTP transport
tools:
  mcp:
    - name: string
      transport:
        type: "http"
        url: string
        authentication: object
      tool_filter: object   # optional allow/deny lists
```

```yaml
# STDIO transport
tools:
  mcp:
    - name: string
      transport:
        type: "stdio"
        command: string
        args: [string]
        env: {string: string}
```

### Execution

```yaml
max_iterations: 20   # max iterations per run; implementations SHOULD respect it and terminate gracefully
```

### Authentication (shared schema)

Used across MCP and webhooks. `type` is one of `bearer`, `jwt`, `oauth2`, `api-key`, `basic`
(bearer requires `token`; basic requires `username`/`password`). Values **SHOULD** use variable
substitution to reference credentials securely rather than hardcoding them.

### Agent skills

On-demand capabilities following the Agent Skills standard.

```yaml
skills:
  - type: local
    path: string   # directory holding a single skill or multiple skill subdirectories
```

Implementations **SHOULD** follow the progressive-disclosure model: load only each skill's `name`
and `description` at startup.

---

## 4. Variable substitution

| Prefix | Context | Resolution | Example |
| --- | --- | --- | --- |
| `env:` | Static | Environment variables at load time | `${env:API_TOKEN}` |
| `http:payload` | Runtime (webhook) | Webhook payload fields per invocation | `${http:payload.event}` |
| `http:header` | Runtime (webhook) | HTTP headers per invocation | `${http:header.User-Agent}` |

**Payload access patterns:**

- Nested objects (dot notation): `${http:payload.field.nested}`
- Special characters (bracket notation): `${http:payload['field.with.dots']}`
- Array indexing: `${http:payload.items[0]}`
- Combined: `${http:payload.users[0].name}`
- Root payload: `${http:payload}`

**Header access** is case-insensitive (`${http:header.Content-Type}` == `${http:header.content-type}`)
and supports special characters (`${http:header.X-GitHub-Event}`).

Implementations **MAY** define additional substitution conventions (e.g., `file:`, `secret:`).

---

## 5. Complete example — "Math Tutor"

```markdown
---
spec_version: "0.4.0"
name: "Math Tutor"
description: "An AI assistant that helps with math problems"
version: "1.0.0"
max_iterations: 20
interfaces:
  - type: consolechat
model:
  name: "gpt-4o"
  provider: "openai"
  authentication:
    type: "api-key"
    api_key: "${env:OPENAI_API_KEY}"
tools:
  mcp:
    - name: "math_operations"
      transport:
        type: "http"
        url: "${env:MATH_MCP_SERVER}"
---

# Role

You are an experienced math tutor capable of assisting students with mathematics problems, providing explanations, step-by-step solutions, and practice exercises.

# Instructions

You are a knowledgeable and patient math tutor who helps students understand mathematical concepts. Provide clear, step-by-step explanations for math problems, using simple language and avoiding jargon unless explaining it.

When solving problems, show all work and explain each step. Use the available math operations tools when performing calculations. Explain mathematical concepts with real-world examples when possible. Be encouraging and supportive of students' efforts, ask clarifying questions if a problem is not clearly stated, provide multiple approaches to solving problems when applicable, help students identify and correct their mistakes.
```

---

## 6. Roadmap (planned, non-normative)

- OpenAPI-based tools integration.
- Multi-agent interaction via the Agent-to-Agent (A2A) protocol.
- Agent memory abstraction.
- Agent Identity support.
- Additional interface types (scheduled execution, REST API).
- Remote Agent Skills from URLs and registries.

---

## 7. Applying AFM to Doxa

AFM is a natural fit for Doxa because the platform already exposes governed capabilities over MCP
(see [mintmcp-gateway-recreation-prompt.md](mintmcp-gateway-recreation-prompt.md) and
[profisee-mcp-mdm-recreation-prompt.md](profisee-mcp-mdm-recreation-prompt.md)). An AFM file lets us
declare an agent once and point it at the Doxa MCP gateway, with all secrets injected via `${env:...}`
substitution rather than hardcoded — consistent with the Aspire-parameter / Keycloak-token posture
described in [doxa-enterprise-architecture-compliance-spec.md](doxa-enterprise-architecture-compliance-spec.md).

> **Secret sourcing.** The `${env:...}` values below are supplied at load time from the same secure
> channels Doxa already uses — .NET Aspire parameters (dev: user secrets; prod: Key Vault). The
> bearer token is a Keycloak-issued access token for the `doxa` realm; the gateway URL resolves to
> the governed MCP endpoint, never a tenant's raw database.

### 7.1 Example — Doxa Support Agent (`doxa-support.afm.md`)

A conversational agent that answers subscriber questions, grounded **only** in governed master data
and limited to read-only tools.

```markdown
---
spec_version: "0.4.0"
name: "Doxa Support Agent"
description: "Answers enterprise subscriber questions grounded in governed Doxa master data"
version: "0.1.0"
authors:
  - "Doxa Platform Team <platform@pizon.com>"
max_iterations: 15
model:
  name: "claude-opus-4-8"
  provider: "anthropic"
  authentication:
    type: "api-key"
    api_key: "${env:ANTHROPIC_API_KEY}"
interfaces:
  - type: webchat
tools:
  mcp:
    - name: "doxa_master_data"
      transport:
        type: "http"
        url: "${env:DOXA_MCP_GATEWAY_URL}"
        authentication:
          type: "bearer"
          token: "${env:DOXA_GATEWAY_TOKEN}"
      tool_filter:
        allow:
          - "search_master_data"
          - "get_golden_record"
          - "get_record_sources"
          - "get_audit_trail"
skills:
  - type: local
    path: "./skills/doxa-support"
---

# Role

You are the Doxa Support Agent. You help enterprise subscribers understand their own organization's
trusted master data (customers, products, suppliers) and the provenance behind each golden record.

# Instructions

Answer only from governed master data retrieved through the available tools — never guess or invent
values. For any factual claim about a record, cite the golden record id and, when asked "why", use
`get_record_sources` to explain the survivorship decision. You have read-only access: never attempt
to modify data. If a question falls outside the caller's tenant or requires data you cannot retrieve,
say so plainly and stop rather than speculating. Keep answers concise and link each figure to its
source record.
```

### 7.2 Example — Doxa Data-Quality Steward (`doxa-dq-steward.afm.md`)

An event-driven agent triggered by the MDM platform's data-quality webhook. It triages an exception
using runtime `${http:payload...}` substitution and may propose a fix (which the gateway still routes
through governance + human approval).

```markdown
---
spec_version: "0.4.0"
name: "Doxa Data-Quality Steward"
description: "Triages master-data quality exceptions raised by the Doxa MDM platform"
version: "0.1.0"
max_iterations: 25
model:
  name: "claude-sonnet-4-6"
  provider: "anthropic"
  authentication:
    type: "api-key"
    api_key: "${env:ANTHROPIC_API_KEY}"
interfaces:
  - type: webhook
    exposure:
      path: "/hooks/mdm-exception"
    prompt: |
      A data-quality exception was raised for tenant ${http:payload.tenantId}.
      Entity: ${http:payload.entity}, record: ${http:payload.recordId}.
      Reported issue: ${http:payload.issue}.
      Triage the exception and recommend a resolution.
    subscription:
      protocol: "webhook"
      authentication:
        type: "bearer"
        token: "${env:DOXA_WEBHOOK_SECRET}"
tools:
  mcp:
    - name: "doxa_master_data"
      transport:
        type: "http"
        url: "${env:DOXA_MCP_GATEWAY_URL}"
        authentication:
          type: "bearer"
          token: "${env:DOXA_GATEWAY_TOKEN}"
      tool_filter:
        allow:
          - "get_golden_record"
          - "get_match_candidates"
          - "run_data_quality_check"
          - "propose_change"
          - "resolve_exception"
---

# Role

You are the Doxa Data-Quality Steward. You triage attribute-level data-quality exceptions on golden
records and recommend (or propose) corrections within the bounds of platform governance.

# Instructions

For each incoming exception, first run `run_data_quality_check` on the affected record to confirm the
issue is still live, then inspect the record with `get_golden_record` and review duplicates with
`get_match_candidates`. When a correction is clear and low-risk, submit it with `propose_change`;
otherwise route it for human review with `resolve_exception` and explain your reasoning. Never assume
write access bypasses approval — every `propose_change` is governance-checked and may require a
steward's sign-off. Operate strictly within the tenant identified by `${http:payload.tenantId}` and
stop if the payload is incomplete.
```

> The MCP tool names above (`search_master_data`, `get_golden_record`, `propose_change`,
> `resolve_exception`, etc.) match the tool surface defined in the Profisee MDM recreation prompt, so
> these agents drop straight onto that gateway.

---

## References

- [Agent-Flavored Markdown — Specification (WSO2, canonical source)](https://wso2.github.io/agent-flavored-markdown/specification/)
- [RFC 2119 — Key words for use in RFCs to Indicate Requirement Levels](https://www.rfc-editor.org/rfc/rfc2119)
- [RFC 8174 — Ambiguity of Uppercase vs Lowercase in RFC 2119 Key Words](https://www.rfc-editor.org/rfc/rfc8174)
