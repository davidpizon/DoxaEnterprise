# Enterprise Resilience & Application Security Blueprint

This document sets up the automated traffic failover patterns required to sustain our uptime SLA and
outlines the static analysis guardrails needed to secure tenant-submitted application code.

> **Implementation note.** This is the hardened, deployable form of the blueprint. Relative to a first
> draft it corrects: Front Door origin `hostName` values (they must be bare FQDNs/IPs and **unique per
> origin**[^afd-origin], not `://`-prefixed duplicates); a missing **route** (without one no traffic
> flows) and the unused Premium **WAF** policy the SKU is paid for; the deprecated `returntocorp/semgrep`
> image (now `semgrep/semgrep`); SCA that only scanned npm even though Doxa is .NET (added a NuGet CVE
> gate); and a custom Semgrep rule that could never match because `...` is an AST ellipsis, not an
> in-string wildcard[^semgrep-ellipsis] (rewritten with `metavariable-regex`). Routing is **active-passive
> priority failover**, consistent with the diagram and the `RTO < 1 hour` goal.

---

## 1. Disaster Recovery Playbook: Regional Failover Orchestration via Azure Front Door

To meet our target metric of `RTO < 1 hour`, this Bicep template defines a global Azure Front Door
(Premium) load-balancing topology. It uses instant-failover health probes to redirect global
application traffic if an entire Azure region suffers a catastrophic blackout.

```bicep
metadata description = 'Deploys a multi-region global Azure Front Door configuration with active-passive priority failover and instant health-probe-driven origin failover.'

param frontDoorProfileName string = 'afd-doxa-enterprise-global'
param endpointName string = 'doxa-prod-endpoint'

@description('Region-specific origin FQDNs. Must be unique per origin and contain no URI scheme.')
param primaryOriginHost string = 'eastus2.app.doxaenterprise.com'
param secondaryOriginHost string = 'centralus.app.doxaenterprise.com'

@description('Canonical Host header the application expects, sent to whichever regional origin serves the request.')
param appHostHeader string = 'app.doxaenterprise.com'

// 1. Define the Global Edge Routing Profile
resource afdProfile 'Microsoft.Cdn/profiles@2023-05-01' = {
  name: frontDoorProfileName
  location: 'Global'
  sku: {
    name: 'Premium_AzureFrontDoor' // Required for advanced WAF rule sets and private link origins
  }
}

// 2. Define the Edge Traffic Ingestion Point
resource afdEndpoint 'Microsoft.Cdn/profiles/afdendpoints@2023-05-01' = {
  parent: afdProfile
  name: endpointName
  location: 'Global'
  properties: {
    enabledState: 'Enabled'
  }
}

// 3. Define the Multi-Region Backend Infrastructure Pool (Origin Group)
resource afdOriginGroup 'Microsoft.Cdn/profiles/origingroups@2023-05-01' = {
  parent: afdProfile
  name: 'og-doxa-compute-clusters'
  properties: {
    loadBalancingSettings: {
      sampleSize: 4
      successfulSamplesRequired: 3
      additionalLatencyInMilliseconds: 50
    }
    healthProbeSettings: {
      probePath: '/healthz' // Automated diagnostic check on underlying clusters
      probeRequestType: 'HEAD'
      probeProtocol: 'Https'
      probeIntervalInSeconds: 15 // Check health every 15 seconds to meet sub-minute recovery thresholds
    }
  }
}

// Primary Region Backend - East US 2 (bare FQDN, no scheme; unique per origin)
resource afdOriginPrimary 'Microsoft.Cdn/profiles/origingroups/origins@2023-05-01' = {
  parent: afdOriginGroup
  name: 'origin-eastus2-primary'
  properties: {
    hostName: primaryOriginHost
    httpPort: 80
    httpsPort: 443
    originHostHeader: appHostHeader
    priority: 1 // Lowest number = target of choice
    weight: 1000
    enabledState: 'Enabled'
  }
}

// Secondary Region Backend - Central US Failover Target
resource afdOriginSecondary 'Microsoft.Cdn/profiles/origingroups/origins@2023-05-01' = {
  parent: afdOriginGroup
  name: 'origin-centralus-secondary'
  properties: {
    hostName: secondaryOriginHost
    httpPort: 80
    httpsPort: 443
    originHostHeader: appHostHeader
    priority: 2 // Active-passive: traffic moves here only when Priority 1 health checks drop
    weight: 1000
    enabledState: 'Enabled'
  }
}

// 4. Premium WAF policy so the SKU's WAF capability is actually enforced.
resource wafPolicy 'Microsoft.Network/frontdoorWebApplicationFirewallPolicies@2024-02-01' = {
  name: 'wafdoxaglobal'
  location: 'Global'
  sku: {
    name: 'Premium_AzureFrontDoor'
  }
  properties: {
    policySettings: {
      enabledState: 'Enabled'
      mode: 'Prevention'
    }
    managedRules: {
      managedRuleSets: [
        {
          ruleSetType: 'Microsoft_DefaultRuleSet'
          ruleSetVersion: '2.1'
          ruleSetAction: 'Block'
        }
      ]
    }
  }
}

// 5. Route binding the endpoint to the origin group. Without a route, no traffic is forwarded.
resource afdRoute 'Microsoft.Cdn/profiles/afdendpoints/routes@2023-05-01' = {
  parent: afdEndpoint
  name: 'route-doxa-default'
  properties: {
    originGroup: {
      id: afdOriginGroup.id
    }
    supportedProtocols: [ 'Https' ]
    patternsToMatch: [ '/*' ]
    forwardingProtocol: 'HttpsOnly'
    httpsRedirect: 'Enabled'
    linkToDefaultDomain: 'Enabled'
  }
  dependsOn: [
    afdOriginPrimary
    afdOriginSecondary
  ]
}

// 6. Security policy associating the WAF policy with the endpoint domain.
resource afdSecurityPolicy 'Microsoft.Cdn/profiles/securitypolicies@2023-05-01' = {
  parent: afdProfile
  name: 'sp-doxa-waf'
  properties: {
    parameters: {
      type: 'WebApplicationFirewall'
      wafPolicy: {
        id: wafPolicy.id
      }
      associations: [
        {
          domains: [
            {
              id: afdEndpoint.id
            }
          ]
          patternsToMatch: [ '/*' ]
        }
      ]
    }
  }
}
```

### 1.1 Regional Outage Routing State Shift

```text
[Subscriber Global Endpoint Traffic]
                 │
                 ▼
      [Azure Front Door Layer]
                 │
      ┌──────────┴──────────┐
      ▼ (Health: 200 OK)    ▼ (Health: 503 Critical Fail)
[Primary Origin Pool]  [Secondary Failover Pool]
  (Priority 1: East US)  (Priority 2: Central US)
      │                     │
      ▼                     ▼
[Active SQL Cluster]   [AlwaysOn Replica Promoting to Primary]
```

---

## 2. Application Security: Static Application Security Testing (SAST) Guardrails

To preserve our SOC 2 Type II and HIPAA data isolation boundaries, code artifacts or integrations
created by tenants must pass through automated quality check gates before being compiled into
multi-tenant infrastructure.

This GitHub Actions workflow utilizes the Semgrep static engine for SAST and pairs it with software
composition analysis (SCA) over both the .NET/NuGet dependencies (the Doxa platform stack) and any
JavaScript front-end assets. It checks for common code injection flaws, vulnerable software
dependencies, and hardcoded secrets.

```yaml
name: "Doxa Enterprise: Application SAST & Guardrails"
on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]
jobs:
  static_analysis_scanning:
    name: "Security Vulnerability Scan"
    runs-on: ubuntu-latest
    container:
      image: semgrep/semgrep # Maintained Semgrep image (returntocorp/semgrep is deprecated)

    steps:
      - name: Checkout Application Source Repository
        uses: actions/checkout@v4

      - name: Run Multi-Tenant SQL Injection & Isolation Checks
        # Targets code mistakes where developers bypass Tenant ID filters during direct queries.
        # 'semgrep ci' is the diff-aware managed alternative when a SEMGREP_APP_TOKEN is configured.
        run: |
          semgrep scan \
            --config "p/owasp-top-10" \
            --config "p/sql-injection" \
            --config "./semgrep.yaml" \
            --error
      - name: Run Secret Detection Scan
        # Blocks hardcoded cryptographic key materials, passwords, or cloud tokens from escaping
        run: |
          semgrep scan \
            --config "p/secrets" \
            --error
  dependency_vulnerability_audit:
    name: "Software Composition Analysis (SCA)"
    runs-on: ubuntu-latest
    steps:
      - name: Checkout Source Repository
        uses: actions/checkout@v4

      # --- .NET / NuGet (the Doxa platform stack) ---
      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore NuGet Dependencies
        run: dotnet restore

      - name: Audit NuGet Dependencies (HIPAA Boundary Verification)
        # 'dotnet list package --vulnerable' exits 0 even when it finds CVEs, so fail explicitly
        # if any High or Critical advisories appear in production or transitive dependencies.
        run: |
          dotnet list package --vulnerable --include-transitive 2>&1 | tee nuget-vulns.txt
          if grep -E "Critical|High" nuget-vulns.txt; then
            echo "::error::High or Critical NuGet vulnerabilities detected."
            exit 1
          fi

      # --- JavaScript / npm (front-end assets) ---
      - name: Setup Secure Platform Language Target
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm' # Requires a committed package-lock.json / npm-shrinkwrap.json

      - name: Audit Third-Party npm Dependencies
        # Blocks the build pipeline if any production software dependencies have known open CVEs
        run: npm audit --audit-level=high
```

### 2.1 Enforced Code-Quality Rule Example (`semgrep.yaml`)

This custom check targets database query calls whose SQL string performs a `SELECT ... FROM ... WHERE`
without an explicit `TenantId` predicate. Because Semgrep's `...` operator works on the AST and **does
not** expand inside string literals, the query string is bound to a metavariable and inspected with
`metavariable-regex` (a negative lookahead asserts `TenantId` is absent).

A second, companion rule (`doxa-flag-concatenated-sql`) flags SQL assembled by **string
concatenation** (`"... WHERE " + ...`). Concatenation is both a classic injection vector and an
isolation hazard — once a query is built from runtime fragments, no static rule can verify the
`TenantId` predicate is present, so the construction itself is rejected in favor of parameterized,
repository-scoped queries.

```yaml
rules:
  - id: doxa-enforce-tenant-isolation-clause
    patterns:
      - pattern: $DB.Execute($QUERY)
      - metavariable-regex:
          metavariable: $QUERY
          # Flags a SELECT ... FROM ... WHERE string that has no TenantId predicate.
          # (?is): case-insensitive, dot matches newlines. Lookaheads require SELECT/FROM/WHERE;
          # the negative lookahead (?!.*tenantid) rejects queries that already scope by TenantId.
          regex: (?is)^(?=.*\bselect\b)(?=.*\bfrom\b)(?=.*\bwhere\b)(?!.*tenantid).*$
    message: "CRITICAL ISOLATION FAULT: Every database command must explicitly contain a TenantId match constraint to prevent cross-customer information leakage under SOC 2 requirements."
    languages: [go, python, javascript, typescript]
    severity: ERROR

  - id: doxa-flag-concatenated-sql
    patterns:
      # Match a DB execute/query call whose argument is a '+' concatenation.
      - pattern-either:
          - pattern: $DB.Execute($SQL + $TAINT)
          - pattern: $DB.Execute($TAINT + $SQL)
          - pattern: $DB.Query($SQL + $TAINT)
          - pattern: $DB.Query($TAINT + $SQL)
      # Only fire when one operand looks like SQL, to suppress noise on ordinary string building.
      # (?is): case-insensitive, dot matches newlines. The regex runs on the source text of $SQL,
      # which — for nested concatenations — includes the full left/right sub-expression.
      - metavariable-regex:
          metavariable: $SQL
          regex: (?is).*\b(select|insert|update|delete|from|where)\b.*
    message: "LIKELY SQL INJECTION + TENANT ISOLATION BYPASS: SQL built by string concatenation. Concatenating runtime values into a query both invites injection and makes the mandatory TenantId predicate impossible to verify statically. Use parameterized queries via the tenant-scoped repository layer instead."
    languages: [go, python, javascript, typescript]
    severity: ERROR
```

> **Scope & isolation-phase caveat.** This rule matches raw SQL passed as a string literal to a
> `*.Execute(...)` call; it does not reason about parameterized queries or ORM/LINQ expression trees.
> It enforces the **current (development) isolation model** — a single shared PostgreSQL database
> scoped by a `TenantId` column plus Row-Level Security. In the **production target**, isolation
> shifts to **database-per-tenant** with per-tenant connection routing (see
> [multi-tenant-cicd-data-isolation-architecture-plan.md](multi-tenant-cicd-data-isolation-architecture-plan.md));
> there the `TenantId` predicate is no longer the primary boundary but remains a defense-in-depth
> backstop for shared catalog/global tables. In **both** phases the durable control is a
> repository-layer abstraction that injects the correct tenant scope centrally — a row filter now, a
> per-tenant connection later — with this SAST rule guarding hand-written queries that bypass it.

---

## Footnotes / Reference Links

[^afd-origin]: [AFDOrigin — `hostName` (Azure CDN/Front Door SDK reference)](https://learn.microsoft.com/dotnet/api/azure.resourcemanager.cdn.frontdoororigindata.hostname). "The address of the origin. Domain names, IPv4 addresses, and IPv6 addresses are supported. This should be unique across all origins in an endpoint." A URI scheme (`://`) is not a valid origin address.
[^semgrep-ellipsis]: [Semgrep pattern syntax — Ellipsis operator](https://semgrep.dev/docs/writing-rules/pattern-syntax). The `...` ellipsis abstracts a sequence of AST nodes (arguments, statements, etc.); it does not act as a wildcard inside the characters of a string literal. To constrain the contents of a matched string, bind it to a metavariable and use `metavariable-regex`.
