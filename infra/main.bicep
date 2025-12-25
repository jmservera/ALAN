// Main Bicep template for ALAN deployment
// This file defines default parameters and orchestrates the main deployment

targetScope = 'subscription'

// ==================================
// Parameters
// ==================================

@minLength(1)
@maxLength(54)
@description('Name of the environment (e.g., dev, staging, prod)')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Name of the resource group (if empty, will be generated)')
param resourceGroupName string = ''

@description('Id of the principal to assign roles to (e.g., your user id or service principal id)')
param principalId string = ''

@description('Type of the principal (User, ServicePrincipal, Group)')
@allowed([
  'User'
  'ServicePrincipal'
  'Group'
])
param principalType string = 'User'

@description('IP address to whitelist for accessing Azure services (e.g., your current public IP)')
param userIpAddress string = ''

// Azure OpenAI Parameters
@description('Azure OpenAI deployment name (model name)')
param openAiDeploymentName string = 'gpt-4o-mini'

@description('Azure OpenAI model name')
param openAiModelName string = 'gpt-4o-mini'

@description('Azure OpenAI model version')
param openAiModelVersion string = '2024-07-18'

@description('Azure OpenAI model capacity (in thousands of tokens per minute)')
param openAiModelCapacity int = 100

// Application Parameters
@description('Maximum loops per day for the agent')
param agentMaxLoopsPerDay int = 4000

@description('Maximum tokens per day for the agent')
param agentMaxTokensPerDay int = 8000000

@description('Agent think interval in seconds')
param agentThinkInterval int = 5

@description('Tags to apply to all resources')
param tags object = {}

// Optional reliability features
@description('Enable zone redundancy for production workloads')
param enableZoneRedundancy bool = false

@description('Enable autoscaling for container apps')
param enableAutoScaling bool = false

@description('Minimum replica count for container apps')
param minReplicas int = 1

@description('Maximum replica count for container apps (only used if enableAutoScaling is true)')
param maxReplicas int = 10

// Container Image Parameters
@description('Container image for agent service (use mcr.microsoft.com/azuredocs/containerapps-helloworld:latest as placeholder)')
param agentContainerImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

@description('Container image for chatapi service (use mcr.microsoft.com/azuredocs/containerapps-helloworld:latest as placeholder)')
param chatApiContainerImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

@description('Container image for web service (use mcr.microsoft.com/azuredocs/containerapps-helloworld:latest as placeholder)')
param webContainerImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

@description('GitHub MCP PAT for the agent to access repositories')
param github_mcp_pat string
@description('GitHub project URL for the agent to access repositories')
param github_project_url string = 'jmservera/ALAN'

// ==================================
// Variables
// ==================================

var abbrs = loadJsonContent('./abbreviations.json')
var generatedResourceGroupName = !empty(resourceGroupName) ? resourceGroupName : '${abbrs.resourcesResourceGroups}${environmentName}'
var commonTags = union(tags, {
  'azd-env-name': environmentName
  environment: environmentName
  project: 'ALAN'
})

// ==================================
// Resources
// ==================================

// Resource Group
resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: generatedResourceGroupName
  location: location
  tags: commonTags
}

// Main deployment
module resources './resources.bicep' = {
  name: 'resources-${environmentName}'
  scope: rg
  params: {
    environmentName: environmentName
    location: location
    principalId: principalId
    principalType: principalType
    userIpAddress: userIpAddress
    openAiDeploymentName: openAiDeploymentName
    openAiModelName: openAiModelName
    openAiModelVersion: openAiModelVersion
    openAiModelCapacity: openAiModelCapacity
    agentMaxLoopsPerDay: agentMaxLoopsPerDay
    agentMaxTokensPerDay: agentMaxTokensPerDay
    agentThinkInterval: agentThinkInterval
    enableZoneRedundancy: enableZoneRedundancy
    enableAutoScaling: enableAutoScaling
    minReplicas: minReplicas
    maxReplicas: maxReplicas
    agentContainerImage: agentContainerImage
    chatApiContainerImage: chatApiContainerImage
    webContainerImage: webContainerImage
    github_mcp_pat: github_mcp_pat
    github_project_url: github_project_url
    tags: commonTags
  }
}

// ==================================
// Outputs
// ==================================

output AZURE_LOCATION string = location
output AZURE_RESOURCE_GROUP string = rg.name
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_SUBSCRIPTION_ID string = subscription().subscriptionId

// Azure OpenAI outputs for local development
output AZURE_OPENAI_ENDPOINT string = resources.outputs.openAiEndpoint
output AZURE_OPENAI_DEPLOYMENT string = openAiDeploymentName

// Storage outputs for local development
output AZURE_STORAGE_ACCOUNT_NAME string = resources.outputs.storageAccountName
output AZURE_STORAGE_CONNECTION_STRING string = resources.outputs.storageConnectionString

// Container Apps outputs
output WEB_APP_URL string = resources.outputs.webAppUrl
output CHATAPI_URL string = resources.outputs.chatApiUrl

// Identity outputs
output AZURE_MANAGED_IDENTITY_CLIENT_ID string = resources.outputs.managedIdentityClientId
output AZURE_MANAGED_IDENTITY_PRINCIPAL_ID string = resources.outputs.managedIdentityPrincipalId

// Container Registry outputs
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.containerRegistryEndpoint
output AZURE_CONTAINER_REGISTRY_NAME string = resources.outputs.containerRegistryName

// Network outputs
output AZURE_VNET_NAME string = resources.outputs.vnetName
output AZURE_VNET_ID string = resources.outputs.vnetId

// Log Analytics outputs
output AZURE_LOG_ANALYTICS_WORKSPACE_ID string = resources.outputs.logAnalyticsWorkspaceId
output AZURE_LOG_ANALYTICS_WORKSPACE_NAME string = resources.outputs.logAnalyticsWorkspaceName

// Container Apps Environment outputs
output AZURE_CONTAINER_APPS_ENVIRONMENT_NAME string = resources.outputs.containerAppsEnvironmentName
output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = resources.outputs.containerAppsEnvironmentId
