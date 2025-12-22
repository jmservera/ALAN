// Main resources deployment for ALAN
// Orchestrates all Azure resources using Azure Verified Modules where available

targetScope = 'resourceGroup'

// ==================================
// Parameters
// ==================================

@description('Name of the environment')
param environmentName string

@description('Primary location for all resources')
param location string

@description('Id of the principal to assign roles to')
param principalId string

@description('Type of the principal')
param principalType string

@description('IP address to whitelist for accessing Azure services')
param userIpAddress string

// Azure OpenAI Parameters
@description('Name of the Azure OpenAI deployment used by ALAN')
param openAiDeploymentName string
@description('Azure OpenAI model name for the deployment (e.g., gpt-4o, gpt-4o-mini)')
param openAiModelName string
@description('Azure OpenAI model version for the deployment')
param openAiModelVersion string
@description('Planned capacity or throughput setting for the Azure OpenAI deployment')
param openAiModelCapacity int

// Application Parameters
@description('Maximum number of agent loop iterations allowed per day')
param agentMaxLoopsPerDay int
@description('Maximum number of tokens the agent may consume per day')
param agentMaxTokensPerDay int
@description('Delay between agent loop iterations in seconds')
param agentThinkInterval int

// Reliability Parameters
@description('Enable zone redundancy for supported resources to improve availability')
param enableZoneRedundancy bool
@description('Enable autoscaling for application compute resources')
param enableAutoScaling bool
@description('Minimum number of application replicas when autoscaling is enabled')
param minReplicas int
@description('Maximum number of application replicas when autoscaling is enabled')
param maxReplicas int

// Container Image Parameters
param agentContainerImage string
param chatApiContainerImage string
param webContainerImage string

@description('Tags to apply to all resources')
param tags object

// ==================================
// Variables
// ==================================

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

// Resource names
var managedIdentityName = '${abbrs.managedIdentityUserAssignedIdentities}${environmentName}-${resourceToken}'
var containerRegistryName = '${abbrs.containerRegistryRegistries}${environmentName}${resourceToken}'
var logAnalyticsName = '${abbrs.operationalInsightsWorkspaces}${environmentName}-${resourceToken}'
var containerAppsEnvName = '${abbrs.appManagedEnvironments}${environmentName}-${resourceToken}'
var vnetName = '${abbrs.networkVirtualNetworks}${environmentName}-${resourceToken}'
var storageAccountName = '${abbrs.storageStorageAccounts}${resourceToken}'
var openAiAccountName = '${abbrs.cognitiveServicesAccounts}${environmentName}-${resourceToken}'

// Network configuration
var vnetAddressPrefix = '10.0.0.0/16'
var infrastructureSubnetAddressPrefix = '10.0.0.0/23'
var privateEndpointSubnetAddressPrefix = '10.0.2.0/24'
var containerAppsSubnetAddressPrefix = '10.0.4.0/23'

// ==================================
// Modules - Using Azure Verified Modules (AVM) where available
// ==================================

// User Assigned Managed Identity
module managedIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.4.0' = {
  name: 'managedIdentity-${environmentName}'
  params: {
    name: managedIdentityName
    location: location
    tags: tags
  }
}

// Log Analytics Workspace
module logAnalytics 'br/public:avm/res/operational-insights/workspace:0.9.1' = {
  name: 'logAnalytics-${environmentName}'
  params: {
    name: logAnalyticsName
    location: location
    tags: tags
    skuName: 'PerGB2018'
    dataRetention: 30
  }
}

// Virtual Network with subnets
module vnet 'br/public:avm/res/network/virtual-network:0.5.2' = {
  name: 'vnet-${environmentName}'
  params: {
    name: vnetName
    location: location
    tags: tags
    addressPrefixes: [
      vnetAddressPrefix
    ]
    subnets: [
      {
        name: 'infrastructure-subnet'
        addressPrefix: infrastructureSubnetAddressPrefix
        serviceEndpoints: [
          'Microsoft.Storage'
          'Microsoft.CognitiveServices'
        ]
      }
      {
        name: 'private-endpoint-subnet'
        addressPrefix: privateEndpointSubnetAddressPrefix
        privateEndpointNetworkPolicies: 'Disabled'
      }
      {
        name: 'container-apps-subnet'
        addressPrefix: containerAppsSubnetAddressPrefix
        delegation: 'Microsoft.App/environments'
      }
    ]
  }
}

// Storage Account with private endpoint
module storage 'br/public:avm/res/storage/storage-account:0.14.3' = {
  name: 'storage-${environmentName}'
  params: {
    name: storageAccountName
    location: location
    tags: tags
    skuName: enableZoneRedundancy ? 'Standard_ZRS' : 'Standard_LRS'
    kind: 'StorageV2'
    allowBlobPublicAccess: false
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Deny'
      ipRules: !empty(userIpAddress) ? [
        {
          value: userIpAddress
          action: 'Allow'
        }
      ] : []
    }
    blobServices: {
      containers: [
        {
          name: 'agent-state'
          publicAccess: 'None'
        }
        {
          name: 'short-term-memory'
          publicAccess: 'None'
        }
        {
          name: 'long-term-memory'
          publicAccess: 'None'
        }
      ]
    }
    queueServices: {
      queues: [
        {
          name: 'human-inputs'
        }
      ]
    }
    privateEndpoints: [
      {
        name: 'pe-${storageAccountName}-blob'
        subnetResourceId: vnet.outputs.subnetResourceIds[1] // private-endpoint-subnet
        service: 'blob'
        privateDnsZoneGroup: {
          privateDnsZoneGroupConfigs: [
            {
              privateDnsZoneResourceId: privateDnsZoneBlob.outputs.resourceId
            }
          ]
        }
      }
      {
        name: 'pe-${storageAccountName}-queue'
        subnetResourceId: vnet.outputs.subnetResourceIds[1] // private-endpoint-subnet
        service: 'queue'
        privateDnsZoneGroup: {
          privateDnsZoneGroupConfigs: [
            {
              privateDnsZoneResourceId: privateDnsZoneQueue.outputs.resourceId
            }
          ]
        }
      }
    ]
    roleAssignments: !empty(principalId) ? [
      {
        principalId: managedIdentity.outputs.principalId
        roleDefinitionIdOrName: 'Storage Blob Data Contributor'
        principalType: 'ServicePrincipal'
      }
      {
        principalId: managedIdentity.outputs.principalId
        roleDefinitionIdOrName: 'Storage Queue Data Contributor'
        principalType: 'ServicePrincipal'
      }
      {
        principalId: principalId
        roleDefinitionIdOrName: 'Storage Blob Data Contributor'
        principalType: principalType
      }
      {
        principalId: principalId
        roleDefinitionIdOrName: 'Storage Queue Data Contributor'
        principalType: principalType
      }
    ] : [
      {
        principalId: managedIdentity.outputs.principalId
        roleDefinitionIdOrName: 'Storage Blob Data Contributor'
        principalType: 'ServicePrincipal'
      }
      {
        principalId: managedIdentity.outputs.principalId
        roleDefinitionIdOrName: 'Storage Queue Data Contributor'
        principalType: 'ServicePrincipal'
      }
    ]
  }
}

// Private DNS Zone for Blob Storage
module privateDnsZoneBlob 'br/public:avm/res/network/private-dns-zone:0.6.0' = {
  name: 'privateDnsZone-blob-${environmentName}'
  params: {
    name: 'privatelink.blob.${environment().suffixes.storage}'
    tags: tags
    virtualNetworkLinks: [
      {
        virtualNetworkResourceId: vnet.outputs.resourceId
        registrationEnabled: false
      }
    ]
  }
}

// Private DNS Zone for Queue Storage
module privateDnsZoneQueue 'br/public:avm/res/network/private-dns-zone:0.6.0' = {
  name: 'privateDnsZone-queue-${environmentName}'
  params: {
    name: 'privatelink.queue.${environment().suffixes.storage}'
    tags: tags
    virtualNetworkLinks: [
      {
        virtualNetworkResourceId: vnet.outputs.resourceId
        registrationEnabled: false
      }
    ]
  }
}

// Private DNS Zone for OpenAI
module privateDnsZoneOpenAi 'br/public:avm/res/network/private-dns-zone:0.6.0' = {
  name: 'privateDnsZone-openai-${environmentName}'
  params: {
    name: 'privatelink.openai.azure.com'
    tags: tags
    virtualNetworkLinks: [
      {
        virtualNetworkResourceId: vnet.outputs.resourceId
        registrationEnabled: false
      }
    ]
  }
}

// Private DNS Zone for Container Registry
module privateDnsZoneAcr 'br/public:avm/res/network/private-dns-zone:0.6.0' = {
  name: 'privateDnsZone-acr-${environmentName}'
  params: {
    name: 'privatelink.azurecr.io'
    tags: tags
    virtualNetworkLinks: [
      {
        virtualNetworkResourceId: vnet.outputs.resourceId
        registrationEnabled: false
      }
    ]
  }
}

// Azure OpenAI with private endpoint
module openai 'br/public:avm/res/cognitive-services/account:0.9.1' = {
  name: 'openai-${environmentName}'
  params: {
    name: openAiAccountName
    location: location
    tags: tags
    kind: 'OpenAI'
    sku: 'S0'
    customSubDomainName: openAiAccountName
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
      ipRules: !empty(userIpAddress) ? [
        {
          value: userIpAddress
        }
      ] : []
    }
    deployments: [
      {
        name: openAiDeploymentName
        sku: {
          name: 'Standard'
          capacity: openAiModelCapacity
        }
        model: {
          format: 'OpenAI'
          name: openAiModelName
          version: openAiModelVersion
        }
      }
    ]
    privateEndpoints: [
      {
        name: 'pe-${openAiAccountName}'
        subnetResourceId: vnet.outputs.subnetResourceIds[1] // private-endpoint-subnet
        service: 'account'
        privateDnsZoneGroup: {
          privateDnsZoneGroupConfigs: [
            {
              privateDnsZoneResourceId: privateDnsZoneOpenAi.outputs.resourceId
            }
          ]
        }
      }
    ]
    roleAssignments: !empty(principalId) ? [
      {
        principalId: managedIdentity.outputs.principalId
        roleDefinitionIdOrName: 'Cognitive Services OpenAI User'
        principalType: 'ServicePrincipal'
      }
      {
        principalId: principalId
        roleDefinitionIdOrName: 'Cognitive Services OpenAI User'
        principalType: principalType
      }
    ] : [
      {
        principalId: managedIdentity.outputs.principalId
        roleDefinitionIdOrName: 'Cognitive Services OpenAI User'
        principalType: 'ServicePrincipal'
      }
    ]
  }
}

// Container Registry
module containerRegistry 'br/public:avm/res/container-registry/registry:0.7.1' = {
  name: 'containerRegistry-${environmentName}'
  params: {
    name: containerRegistryName
    location: location
    tags: tags
    acrSku: 'Premium' // Premium SKU required for private endpoints
    publicNetworkAccess: 'Enabled' // Allow public access for image pull during provisioning and for user IP to push
    networkRuleBypassOptions: 'AzureServices'
    networkRuleSetDefaultAction: 'Deny' // Deny by default, allow specific IPs
    networkRuleSetIpRules: !empty(userIpAddress) ? [
      {
        value: userIpAddress
        action: 'Allow'
      }
    ] : []
    exportPolicyStatus: 'enabled' // Required when publicNetworkAccess is Enabled
    privateEndpoints: [
      {
        name: 'pe-${containerRegistryName}'
        subnetResourceId: vnet.outputs.subnetResourceIds[1] // private-endpoint-subnet
        service: 'registry'
        privateDnsZoneGroup: {
          privateDnsZoneGroupConfigs: [
            {
              privateDnsZoneResourceId: privateDnsZoneAcr.outputs.resourceId
            }
          ]
        }
      }
    ]
    roleAssignments: [
      {
        principalId: managedIdentity.outputs.principalId
        roleDefinitionIdOrName: 'AcrPull'
        principalType: 'ServicePrincipal'
      }
    ]
  }
}

// Container Apps Environment
module containerAppsEnvironment 'br/public:avm/res/app/managed-environment:0.8.2' = {
  name: 'containerAppsEnv-${environmentName}'
  params: {
    name: containerAppsEnvName
    location: location
    tags: tags
    logAnalyticsWorkspaceResourceId: logAnalytics.outputs.resourceId
    infrastructureSubnetId: vnet.outputs.subnetResourceIds[2] // container-apps-subnet
    internal: false // false to allow public access to web app
    zoneRedundant: enableZoneRedundancy
  }
}

// Container App - Agent (internal only)
module agentApp './modules/container-app.bicep' = {
  name: 'containerApp-agent-${environmentName}'
  params: {
    name: '${abbrs.appContainerApps}agent-${environmentName}'
    location: location
    tags: tags
    containerAppsEnvironmentId: containerAppsEnvironment.outputs.resourceId
    managedIdentityId: managedIdentity.outputs.resourceId
    containerRegistryName: containerRegistry.outputs.name
    containerImage: agentContainerImage
    containerPort: 0 // No exposed port for agent
    enableIngress: false
    scalingRuleType: 'cpu' // Use CPU-based scaling for background service
    environmentVariables: [
      {
        name: 'AZURE_CLIENT_ID'
        value: managedIdentity.outputs.clientId
      }
      {
        name: 'AZURE_OPENAI_ENDPOINT'
        value: openai.outputs.endpoint
      }
      {
        name: 'AZURE_OPENAI_DEPLOYMENT'
        value: openAiDeploymentName
      }
      {
        name: 'AGENT_MAX_LOOPS_PER_DAY'
        value: string(agentMaxLoopsPerDay)
      }
      {
        name: 'AGENT_MAX_TOKENS_PER_DAY'
        value: string(agentMaxTokensPerDay)
      }
      {
        name: 'AGENT_THINK_INTERVAL'
        value: string(agentThinkInterval)
      }
      {
        name: 'AZURE_STORAGE_ACCOUNT_NAME'
        value: storage.outputs.name
      }
    ]
    minReplicas: minReplicas
    maxReplicas: enableAutoScaling ? maxReplicas : minReplicas
    cpu: '0.5'
    memory: '1Gi'
  }
}

// Container App - ChatApi (internal only)
module chatApiApp './modules/container-app.bicep' = {
  name: 'containerApp-chatapi-${environmentName}'
  params: {
    name: '${abbrs.appContainerApps}chatapi-${environmentName}'
    location: location
    tags: tags
    containerAppsEnvironmentId: containerAppsEnvironment.outputs.resourceId
    managedIdentityId: managedIdentity.outputs.resourceId
    containerRegistryName: containerRegistry.outputs.name
    containerImage: chatApiContainerImage
    containerPort: 5041
    enableIngress: true
    ingressExternal: false // Internal only
    environmentVariables: [
      {
        name: 'AZURE_CLIENT_ID'
        value: managedIdentity.outputs.clientId
      }
      {
        name: 'AZURE_OPENAI_ENDPOINT'
        value: openai.outputs.endpoint
      }
      {
        name: 'AZURE_OPENAI_DEPLOYMENT'
        value: openAiDeploymentName
      }
      {
        name: 'AZURE_STORAGE_ACCOUNT_NAME'
        value: storage.outputs.name
      }
      {
        name: 'ASPNETCORE_URLS'
        value: 'http://+:5041'
      }
    ]
    minReplicas: minReplicas
    maxReplicas: enableAutoScaling ? maxReplicas : minReplicas
    cpu: '0.5'
    memory: '1Gi'
  }
}

// Container App - Web (public)
module webApp './modules/container-app.bicep' = {
  name: 'containerApp-web-${environmentName}'
  params: {
    name: '${abbrs.appContainerApps}web-${environmentName}'
    location: location
    tags: tags
    containerAppsEnvironmentId: containerAppsEnvironment.outputs.resourceId
    managedIdentityId: managedIdentity.outputs.resourceId
    containerRegistryName: containerRegistry.outputs.name
    containerImage: webContainerImage
    containerPort: 5269
    enableIngress: true
    ingressExternal: true // Public access
    environmentVariables: [
      {
        name: 'NODE_ENV'
        value: 'production'
      }
      {
        name: 'PORT'
        value: '5269'
      }
      {
        name: 'CHATAPI_URL'
        value: 'https://${chatApiApp.outputs.fqdn}/api'
      }
      {
        name: 'NEXT_PUBLIC_CHATAPI_URL'
        value: 'https://${chatApiApp.outputs.fqdn}/api'
      }
    ]
    minReplicas: minReplicas
    maxReplicas: enableAutoScaling ? maxReplicas : minReplicas
    cpu: '0.5'
    memory: '1Gi'
  }
}

// ==================================
// Outputs
// ==================================

// Identity outputs
output managedIdentityClientId string = managedIdentity.outputs.clientId
output managedIdentityPrincipalId string = managedIdentity.outputs.principalId

// Storage outputs
output storageAccountName string = storage.outputs.name
// Note: Managed identity connection string (no AccountKey); storage access keys are not available when using private endpoints
output storageConnectionString string = 'DefaultEndpointsProtocol=https;AccountName=${storage.outputs.name};EndpointSuffix=${environment().suffixes.storage}'

// OpenAI outputs
output openAiEndpoint string = openai.outputs.endpoint
output openAiName string = openai.outputs.name

// Container Apps outputs
output webAppUrl string = 'https://${webApp.outputs.fqdn}'
output chatApiUrl string = 'https://${chatApiApp.outputs.fqdn}'

// Container Registry outputs
output containerRegistryName string = containerRegistry.outputs.name
output containerRegistryEndpoint string = containerRegistry.outputs.loginServer
