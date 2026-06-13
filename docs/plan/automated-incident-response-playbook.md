# Automated Incident Response Playbook

This deployment template provides an automated incident response (SOAR) solution for Doxa Enterprise
using Azure Logic Apps (Standard). When the Microsoft Sentinel analytics rule fires — identifying five
or more unauthorized cross-tenant data access failures within 15 minutes (see the KQL rule in
[enterprise-governance-security-operations-plan.md](enterprise-governance-security-operations-plan.md)) —
it triggers this workflow to immediately isolate the source IP address at the **Application Gateway WAF**
and revoke the attacker's authentication sessions in Microsoft Entra ID.

> **Implementation note.** This document is the hardened, deployable form of the playbook. Relative to a
> first draft, it corrects several issues that would otherwise block deployment or runtime: Logic Apps
> Standard requires a backing **storage account** and Functions app settings; managed-identity HTTP
> actions require the host to actually declare a **system-assigned identity** plus RBAC/Graph
> permissions; an **Application Security Group cannot block an IP** (ASGs only group NICs/VMs as
> NSG-rule sources/destinations[^asg]) so isolation is performed via a **WAF policy custom rule**; and
> the placeholder `https://azure.com` / `https://microsoft.com` endpoints are replaced with the real
> Azure Resource Manager and Microsoft Graph URIs.

---

## 1. Logic App Infrastructure Definition (Bicep)

```bicep
metadata description = 'Deploys an automated incident response Logic App (Standard) Playbook integrated with Microsoft Sentinel.'

param location string = resourceGroup().location
param playbookName string = 'logic-doxa-security-incident-response'
param appServicePlanName string = 'plan-doxa-security-automation'

@description('Name of the existing Application Gateway WAF policy whose custom rules the playbook updates to block attacker IPs.')
param wafPolicyName string = 'waf-doxa-prod-edge'

// Globally-unique storage account backing the Logic App Standard runtime (state, run history, content share).
var storageAccountName = 'stdoxasoar${uniqueString(resourceGroup().id)}'

// Built-in role definition IDs (tenant-wide, stable GUIDs).
var networkContributorRoleId = '4d97b98b-1d4f-4787-a291-c67834d212e7'
var storageAccountContributorRoleId = '17d1049b-9a84-46fb-8f53-869881c3d3ab'
var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageQueueDataContributorRoleId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var storageTableDataContributorRoleId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'

// 1. Storage account required by every Logic Apps Standard / Functions host.
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_3' // Platform baseline: TLS 1.3 everywhere (compliance spec §3.1)
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    // Note: shared-key access cannot be disabled on the Workflow Service Plan; the runtime content
    // share (Azure Files) still requires a key-based connection string. The data-plane runtime
    // (AzureWebJobsStorage) is moved to identity-based auth below.
  }
}

// 2. User-assigned identity used specifically for identity-based access to the runtime storage account.
resource storageIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-doxa-soar-storage'
  location: location
}

// 3. Grant the storage identity the roles Logic Apps Standard requires for identity-based AzureWebJobsStorage.
resource storageRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for roleId in [
  storageAccountContributorRoleId
  storageBlobDataOwnerRoleId
  storageQueueDataContributorRoleId
  storageTableDataContributorRoleId
]: {
  name: guid(storageAccount.id, storageIdentity.id, roleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleId)
    principalId: storageIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}]

// 4. Dedicated Workflow Standard (WS1) plan required to run Logic Apps Standard workflows.
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'WS1'
    tier: 'WorkflowStandard'
  }
  properties: {
    targetWorkerCount: 1
    maximumElasticWorkerCount: 20
  }
}

// 5. Logic App Standard workflow host. The system-assigned identity authenticates the outbound HTTP
//    actions (WAF policy + Microsoft Graph); the user-assigned identity authenticates runtime storage.
resource logicAppWorkflow 'Microsoft.Web/sites@2023-01-01' = {
  name: playbookName
  location: location
  kind: 'functionapp,workflowapp'
  identity: {
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${storageIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0' // .NET 6 is out of support; Logic Apps Standard runs on .NET 8.
      minTlsVersion: '1.3'        // Platform baseline: TLS 1.3 everywhere (compliance spec §3.1)
      scmMinTlsVersion: '1.3'
      functionsRuntimeScaleMonitoringEnabled: true // Required on the WS plan when AzureWebJobsStorage uses identity-based auth.
      appSettings: [
        // Identity-based AzureWebJobsStorage (no account key in the connection): the runtime resolves
        // Blob/Queue/Table via the user-assigned identity's role assignments granted above.
        { name: 'AzureWebJobsStorage__credential', value: 'managedIdentity' }
        { name: 'AzureWebJobsStorage__managedIdentityResourceId', value: storageIdentity.id }
        { name: 'AzureWebJobsStorage__blobServiceUri', value: storageAccount.properties.primaryEndpoints.blob }
        { name: 'AzureWebJobsStorage__queueServiceUri', value: storageAccount.properties.primaryEndpoints.queue }
        { name: 'AzureWebJobsStorage__tableServiceUri', value: storageAccount.properties.primaryEndpoints.table }
        // Content share (Azure Files) still requires a key-based connection string on the WS plan.
        { name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING', value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}' }
        { name: 'WEBSITE_CONTENTSHARE', value: toLower(playbookName) }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'APP_KIND', value: 'workflowApp' }
      ]
    }
  }
  dependsOn: [
    storageRoleAssignments
  ]
}

// 6. Grant the playbook's system-assigned identity permission to modify the WAF policy (Network
//    Contributor, scoped to this resource group). Microsoft Graph 'User.ReadWrite.All' / a directory
//    role for revokeSignInSessions is an app-role assignment granted separately via Microsoft Graph.
resource wafWriteAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, logicAppWorkflow.id, networkContributorRoleId)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', networkContributorRoleId)
    principalId: logicAppWorkflow.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// 7. Sentinel managed (azuresentinel) API connection authenticated with the Logic App's managed identity.
resource sentinelConnection 'Microsoft.Web/connections@2016-06-01' = {
  name: 'azuresentinel-connection'
  location: location
  properties: {
    displayName: 'Sentinel Security Orchestration Engine'
    parameterValueType: 'Alternative' // Use managed-identity auth rather than a stored OAuth credential.
    api: {
      id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'azuresentinel')
    }
  }
}
```

> **Permissions that must be granted out-of-band.** `revokeSignInSessions` requires a Microsoft Graph
> application permission (`User.ReadWrite.All`, or a privileged directory role) assigned to the
> playbook's managed identity. Graph app-role assignments are not expressible as Azure RBAC
> `roleAssignments`; grant them with `New-MgServicePrincipalAppRoleAssignment` (or a deployment script)
> after the identity exists. Scope to least privilege.

---

## 2. Orchestration Sequence Definition (`workflow.json`)

The following definition dictates the sequential steps executed inside the Logic App runtime once
triggered by a Sentinel alert. The subscription ID and WAF policy name are passed in as workflow
parameters — `subscription()` is an ARM-template function and is **not** valid inside a Logic App
expression.

```json
{
  "definition": {
    "$schema": "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#",
    "contentVersion": "1.0.0.0",
    "parameters": {
      "$connections": {
        "type": "Object",
        "defaultValue": {}
      },
      "subscriptionId": {
        "type": "String"
      },
      "resourceGroupName": {
        "type": "String",
        "defaultValue": "rg-doxa-prod"
      },
      "wafPolicyName": {
        "type": "String",
        "defaultValue": "waf-doxa-prod-edge"
      }
    },
    "triggers": {
      "When_Microsoft_Sentinel_Alert_is_triggered": {
        "type": "ApiConnection",
        "inputs": {
          "host": {
            "connection": {
              "name": "@parameters('$connections')['azuresentinel']['connectionId']"
            }
          },
          "method": "post",
          "path": "/alert/v2/trigger"
        }
      }
    },
    "actions": {
      "Extract_Malicious_IP": {
        "type": "InitializeVariable",
        "inputs": {
          "variables": [
            {
              "name": "AttackerIP",
              "type": "string",
              "value": "@triggerBody()?['Entities']?[0]?['Address']"
            }
          ]
        },
        "runAfter": {}
      },
      "Block_Attacker_IP_In_WAF_Policy": {
        "type": "Http",
        "inputs": {
          "method": "PUT",
          "uri": "https://management.azure.com/subscriptions/@{parameters('subscriptionId')}/resourceGroups/@{parameters('resourceGroupName')}/providers/Microsoft.Network/ApplicationGatewayWebApplicationFirewallPolicies/@{parameters('wafPolicyName')}/customRules/BlockDoxaIncidentIPs?api-version=2023-11-01",
          "authentication": {
            "type": "ManagedServiceIdentity",
            "audience": "https://management.azure.com"
          },
          "body": {
            "properties": {
              "priority": 10,
              "ruleType": "MatchRule",
              "action": "Block",
              "matchConditions": [
                {
                  "matchVariables": [
                    { "variableName": "RemoteAddr" }
                  ],
                  "operator": "IPMatch",
                  "matchValues": [
                    "@{variables('AttackerIP')}"
                  ]
                }
              ]
            }
          }
        },
        "runAfter": {
          "Extract_Malicious_IP": [
            "Succeeded"
          ]
        }
      },
      "Revoke_Active_Entra_ID_Sessions": {
        "type": "Http",
        "inputs": {
          "method": "POST",
          "uri": "https://graph.microsoft.com/v1.0/users/@{triggerBody()?['Entities']?[1]?['UserPrincipalName']}/revokeSignInSessions",
          "authentication": {
            "type": "ManagedServiceIdentity",
            "audience": "https://graph.microsoft.com"
          }
        },
        "runAfter": {
          "Block_Attacker_IP_In_WAF_Policy": [
            "Succeeded"
          ]
        }
      },
      "Post_Incident_Notification_to_SOC": {
        "type": "ApiConnection",
        "inputs": {
          "host": {
            "connection": {
              "name": "@parameters('$connections')['azuresentinel']['connectionId']"
            }
          },
          "method": "post",
          "path": "/incident/comment/add",
          "body": {
            "incidentId": "@triggerBody()?['IncidentId']",
            "comment": "AUTOMATED RESPONSE EXECUTION: Proactive mitigation applied. Source IP has been blocked via a WAF policy custom rule at the application edge, and active user sessions have been revoked in Microsoft Entra ID."
          }
        },
        "runAfter": {
          "Revoke_Active_Entra_ID_Sessions": [
            "Succeeded"
          ]
        }
      }
    }
  }
}
```

> **Merge, don't clobber.** Writing a single named custom rule (`BlockDoxaIncidentIPs`) keeps the
> playbook idempotent, but a production playbook should first `GET` the rule, append the new IP to its
> `matchValues`, and `PUT` the merged result so repeated incidents accumulate blocked IPs rather than
> overwriting one another. Pair the rule with a scheduled cleanup that ages out stale entries.

---

## 3. Incident Isolation Sequence Map

```text
    [Microsoft Sentinel Alert: 5+ Cross-Tenant Violations]
                              │
                              ▼
               [Trigger Azure Logic App Playbook]
                              │
             ┌────────────────┴────────────────┐
             ▼                                 ▼
[ARM REST: WAF policy custom rule]   [Microsoft Graph API call]
             │                                 │
             ▼                                 ▼
 [Block IP at Application Edge]       [Revoke Active Entra ID Sessions]
             │                                 │
             └────────────────┬────────────────┘
                              │
                              ▼
            [Log Action as Comment on Sentinel Incident]
```

---

## Footnotes / Reference Links

[^asg]: [Application security groups — Azure Virtual Network](https://learn.microsoft.com/azure/virtual-network/application-security-groups). ASGs group network interfaces and are referenced as the source/destination of NSG rules; they do not hold IP addresses and cannot block traffic on their own.
