# Azure Deployment & Security Implementation Plan

This implementation plan contains both the automated infrastructure code (Bicep/ARM template) for
the secure auditing storage infrastructure and the precise Data Loss Prevention (DLP) configuration
rules required to protect multi-tenant attachments.

---

## 1. Infrastructure as Code: Immutable Audit Storage (Bicep / ARM)

This infrastructure specification is written in Bicep (Azure's native, preferred language for ARM
deployments). It programmatically provisions a zero-trust storage account configured with a strict
**container-level** Write-Once-Read-Many (WORM) time-based retention policy. Protected append writes
are enabled so the system can continuously append audit logs while deletes and edits remain blocked —
satisfying NIST SP 800-53 auditing requirements. The policy is deployed unlocked and becomes
irreversible once locked (see the deployment workflow in Section 3).

> **Why container-level WORM (not version-level)?** Azure offers two immutability models. *Version-level*
> WORM requires blob versioning and does **not** support protected append writes. *Container-level* WORM
> has no versioning prerequisite and **does** support `allowProtectedAppendWrites`, which is the
> append-blob pattern Azure recommends for auditing/logging. Because this store is an append-only audit
> log, the template uses container-level WORM and therefore does **not** set
> `immutableStorageWithVersioning` on the container — enabling both at once would fail deployment.

```bicep
metadata description = 'Deploys a secure, compliant Azure Storage Account with Immutable WORM storage for Doxa Enterprise.'

@description('The Azure region where the secure storage resources will be deployed.')
param location string = resourceGroup().location

@description('The unique naming prefix for the enterprise audit storage resource.')
param storageAccountName string = 'stdoxaaudit${uniqueString(resourceGroup().id)}'

@description('The length of time in days that the audit logs must be kept immutably locked without alteration.')
param retentionPeriodDays int = 2555 // 7 Years Regulatory Compliance Window

resource auditStorageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_GRS' // Geo-Redundant Storage to ensure Multi-Region disaster recovery
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_3' // Strict compliance mandate: drop TLS 1.0, 1.1, and 1.2 at the edge
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false // Strictly block any anonymous external access
    publicNetworkAccess: 'Disabled' // Force traffic through Private Endpoint inside the VNet
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Deny'
    }
    encryption: {
      services: {
        blob: {
          keyType: 'Account'
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage' // Can be updated to Microsoft.KeyVault for Customer-Managed Keys (CMK)
    }
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: auditStorageAccount
  name: 'default'
  properties: {
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 30
    }
  }
}

resource auditContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'immutable-compliance-logs'
  properties: {
    publicAccess: 'None'
    // Container-level WORM: no versioning prerequisite. The time-based retention policy is
    // applied via the child immutabilityPolicies resource below, which supports
    // allowProtectedAppendWrites. Do NOT enable immutableStorageWithVersioning here — that
    // switches the container to version-level WORM, which is incompatible with append writes.
  }
}

resource immutabilityPolicy 'Microsoft.Storage/storageAccounts/blobServices/containers/immutabilityPolicies@2023-01-01' = {
  parent: auditContainer
  name: 'default'
  properties: {
    immutabilityPeriodSinceCreationInDays: retentionPeriodDays
    allowProtectedAppendWrites: true // Crucial: Allows the system to append logs while blocking deletes/edits
  }
}
```

---

## 2. Security Governance: Data Loss Prevention (DLP) Implementation Rule Set

To maintain total HIPAA and SOC 2 data privacy compliance within a multi-tenant platform, Microsoft
Purview DLP rules must scan and neutralize any prohibited Protected Health Information (PHI) or
Personally Identifiable Information (PII) before it is committed to tenant shared directories.

### Policy Configuration Object (JSON Specification)

```json
{
  "policyName": "Doxa-Enterprise-HIPAA-MultiTenant-DLP",
  "description": "Enforces technical safeguards under 45 CFR § 164.312. Prevents cross-tenant leaks of ePHI and unencrypted medical attachments.",
  "state": "Enabled",
  "enforcementMode": "BlockWithOverrideAndJustification",
  "rules": [
    {
      "name": "Detect-and-Restrict-Unencrypted-ePHI",
      "priority": 1,
      "conditions": {
        "and": [
          {
            "sensitiveInformationTypes": [
              { "name": "U.S. Social Security Number (SSN)", "minConfidence": 85 },
              { "name": "U.S. Individual Taxpayer Identification Number (ITIN)", "minConfidence": 85 },
              { "name": "U.S. Medical Terms & Conditions (ICD-9/ICD-10-CM)", "minConfidence": 75 },
              { "name": "DEA Number / National Provider Identifier (NPI)", "minConfidence": 90 }
            ]
          },
          {
            "tenantIsolationContext": "ExternalOrCrossTenantSharingAttempt"
          }
        ]
      },
      "actions": [
        {
          "type": "BlockAccess",
          "target": "AllExceptTenantDataOwner",
          "customErrorMessage": "Access Denied. This file contains Protected Health Information (PHI) and cannot be transferred across enterprise tenant boundaries under HIPAA technical safety standards."
        },
        {
          "type": "ApplySensitivityLabel",
          "labelId": "confidential-hipaa-restricted-uuid"
        }
      ],
      "reporting": {
        "alertSeverity": "High",
        "sendEmailNotification": true,
        "notifyRoles": ["ComplianceAdministrator", "TenantGlobalAdmin"],
        "auditLogSinks": ["AzurePurviewAuditStream", "DoxaEventHubs"]
      }
    },
    {
      "name": "Strict-File-Type-Sanitization",
      "priority": 2,
      "conditions": {
        "or": [
          { "fileExtension": ["exe", "bat", "sh", "dmg", "vbs"] },
          { "unsupportedContentEncryption": true }
        ]
      },
      "actions": [
        {
          "type": "QuarantineFile",
          "quarantineVNetStoragePath": "stquarantine/malicious-attachments"
        }
      ],
      "reporting": {
        "alertSeverity": "Critical",
        "notifyRoles": ["SecurityOperationsCenter"]
      }
    }
  ]
}
```

---

## 3. Operational Deployment Workflow

1. **Deployment Execution:** Execute the Bicep template via the Azure CLI to set up your locked audit
   vault.

   ```bash
   az deployment group create --resource-group rg-doxa-prod --template-file storage.bicep
   ```

2. **Locking Policy:** After verifying log streaming functions correctly, lock the immutability state
   from the CLI. Once locked, even Global Administrators cannot manually override the 7-year retention
   protocol.

3. **DLP Enforcement:** Inject the JSON configuration directly into Microsoft Purview via the Security
   & Compliance PowerShell module or the Purview Management API to instantly protect real-time file
   attachments across all enterprise subscribers.
