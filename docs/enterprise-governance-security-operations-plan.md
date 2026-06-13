# Enterprise Governance & Security Operations Plan

This execution layout outlines the security operations protocols for Doxa Enterprise.[^1] Section 1
details the safe data destruction patterns required under HIPAA and SOC 2.[^2] Section 2 presents the
infrastructure pipeline for exporting continuous security event telemetry to Microsoft Sentinel.[^3]

---

## 1. Runbook: Tenant Offboarding & Certified Data Purging

When an enterprise tenant cancels their subscription, their cryptographic footprints and physical
databases must be completely eliminated to comply with standard HIPAA and SOC 2 retention
policies.[^1][^4]

### Phase 1: Administrative Isolation & Logical Deactivation

**1. Revoke Active Enterprise Access.** Immediately update the tenant's data isolation status inside
the central catalog mapping engine.[^5]

```sql
UPDATE TenantRoutingCatalog 
SET DataIsolationStatus = 'DEACTIVATED' 
WHERE TenantId = '4f8e91a2-63b7-4c12-89d4-e5f6a7b8c9d0';
```

**2. Purge User Access Lifetimes.** Programmatically call the Microsoft Graph API to forcefully
terminate active subscriber authentication sessions across Microsoft Entra ID.[^6]

### Phase 2: Cryptographic Shredding

Because raw storage blocks may persist in backups for standard archival windows, Doxa relies on
Cryptographic Shredding.[^7] Deleting a tenant's exclusive encryption key renders all underlying data
irrecoverable instantly.[^7][^8]

```powershell
# Authenticate securely to the target Production Key Vault Instance
Select-AzSubscription -SubscriptionId "sub-doxa-prod-uuid"

# Purge the Customer-Managed Key (CMK) tied directly to the targeted tenant's database
Remove-AzKeyVaultKey -VaultName "kv-doxa-tenant-keys" -Name "key-tenant-4f8e91a2" -Force

# Purge the soft-deleted key from the recycle bin to block any administrative recovery attempts.
# Requires the 'purge' permission (e.g., the Key Vault Crypto Officer or Key Vault Purge Operator role).
Remove-AzKeyVaultKey -VaultName "kv-doxa-tenant-keys" -Name "key-tenant-4f8e91a2" -InRemovedState -Force
```

> **Compliance note.** Purging requires the `purge` data-action permission and only succeeds if the
> vault does **not** have **purge protection** enabled. Purge protection is strongly recommended for
> production vaults — when it is on, a deleted key cannot be purged early and is auto-purged only
> after the retention window (7–90 days) elapses. Cryptographic erasure is effective at the moment of
> *deletion* (the key can no longer wrap/unwrap the database encryption key); the recycle-bin purge
> step hardens against recovery only where governance policy permits early purge.

### Phase 3: Physical Compute and Database Elimination

**1. Drop Physical Azure SQL Shard.** Completely detach and delete the tenant's isolated data node
from the shared production Elastic Pool.[^5]

```powershell
Remove-AzSqlDatabase -ResourceGroupName "rg-doxa-prod" -ServerName "sql-doxa-pool" -DatabaseName "db_tenant_4f8e91a2"
```

**2. Wipe Sandbox Working Directories.** Issue automated lifecycles to clear isolated caching and
staging folder trees inside unstructured file tiers.[^9]

```powershell
Remove-AzStorageDirectory -Context $storageContext -ShareName "tenant-file-shares" -Path "attachments/4f8e91a2" -Force
```

---

## 2. Infrastructure Code: Real-Time SIEM / Microsoft Sentinel Integration

To fulfill the threat monitoring requirements of SOC 2 Trust Services Criteria Section CC7.0, all
access decisions, API adjustments, and infrastructure changes must feed into Microsoft Sentinel.[^10][^11]

This Bicep template deploys a native data pipeline that connects your Azure Kubernetes Service (AKS)
environment and core network infrastructure to a centralized Log Analytics workspace.[^11]

```bicep
metadata description = 'Deploys a Log Analytics Workspace configured with Microsoft Sentinel and structural security diagnostic log rules.'

param location string = resourceGroup().location
param workspaceName string = 'log-doxa-security-siem'
param sentinelSolutionName string = 'SecurityInsights(log-doxa-security-siem)'

// 1. Provision the foundational Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018' // Flexible pricing model matching varying enterprise audit ingestion traffic
    }
    retentionInDays: 365 // Retain hot searchable logging streams for a full operational compliance year
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

// 2. Enable the Microsoft Sentinel Solution engine inside the Workspace
resource sentinelSolution 'Microsoft.OperationsManagement/solutions@2015-11-01' = {
  name: sentinelSolutionName
  location: location
  plan: {
    name: sentinelSolutionName
    product: 'OMSGallery/SecurityInsights'
    publisher: 'Microsoft'
    promotionCode: ''
  }
  properties: {
    workspaceResourceId: logAnalyticsWorkspace.id
  }
}

// 3. Reference the existing multi-tenant application gateway as the diagnostic target.
//    'scope' must be a resource symbol (the extension-resource pattern), not a string.
resource appGateway 'Microsoft.Network/applicationGateways@2023-11-01' existing = {
  name: 'agw-doxa-prod-edge'
}

// 4. Stream application gateway diagnostics to the workspace. Per-resource log retention is
//    deprecated, so retention is governed by the workspace 'retentionInDays' set above.
resource appGatewayDiagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'ds-app-gateway-to-sentinel'
  scope: appGateway
  properties: {
    workspaceId: logAnalyticsWorkspace.id
    logs: [
      {
        category: 'ApplicationGatewayAccessLog'
        enabled: true
      }
      {
        category: 'ApplicationGatewayFirewallLog' // Captures real-time WAF blocks and tenant probing events
        enabled: true
      }
    ]
  }
}
```

### 2.1 Threat Hunting Rule: Detecting Cross-Tenant Data Anomalies

Once your logs feed into Sentinel, use this Kusto Query Language (KQL) query to spot threat actors
trying to access data across tenant boundaries.[^12]

```kusto
// Targets unexpected spikes in rejected cross-tenant requests 
let Threshold = 5;
TenantAccessLogs_CL
| where ResponseStatus_s == "403_FORBIDDEN" or AccessResult_s == "TENANT_MISMATCH"
| summarize FailedAttempts = count() by ClientIP_s, TargetedTenantId_g, bin(TimeGenerated, 15m)
| where FailedAttempts >= Threshold
| project TimeGenerated, ClientIP_s, TargetedTenantId_g, FailedAttempts
| order by FailedAttempts desc
```

---

## Footnotes / Reference Links

[^1]: Azure Governance for Regulated SaaS Applications — Structural reference frameworks managing multi-tenant privacy models.
[^2]: HIPAA/HITRUST Compliance Mappings on Microsoft Azure — Control parameters enforcing audit logging boundaries.
[^3]: Microsoft Sentinel Security Integration Blueprint — Guide to cloud system tracking.
[^4]: AICPA Trust Services Criteria (SOC 2) Technical Implementations — Security standards guiding configuration tracking.
[^5]: Multi-Tenant Architecture Database Sharding Strategies — Logical segregation parameters handling tenant pools.
[^6]: Microsoft Entra ID Session Termination and Continuous Evaluation — Mechanics governing real-time credential invalidation.
[^7]: Cryptographic Shredding Principles in Secure Data Disks — Technical details regarding instant tenant data purging.
[^8]: Azure Key Vault Customer-Managed Key Operations — Hardening parameters for key retention and life management.
[^9]: Azure Storage Lifecycle Retention Management Policies — Data governance pipelines.
[^10]: NIST SP 800-53 Audit and Accountability (AU) Mapping — Control patterns standardizing SIEM architecture pipelines.
[^11]: Bicep Templates Deployment Schemas for Monitoring Solutions — Structural layout standards for Log Analytics spaces.
[^12]: Kusto Query Language (KQL) Analysis Guide for Cloud Events — Query construction mechanics tracking threat actions.
