@description('Name of the web app')
param webAppName string = 'alan-web'

@description('Name of the agent app')
param agentAppName string = 'alan-agent'

@description('Location for all resources')
param location string = resourceGroup().location

@description('OpenAI API Key')
@secure()
param openAiApiKey string

@description('The SKU of App Service Plan')
param sku string = 'B1'

var appServicePlanName = 'asp-${webAppName}'
var webAppFullName = '${webAppName}-${uniqueString(resourceGroup().id)}'
var agentAppFullName = '${agentAppName}-${uniqueString(resourceGroup().id)}'

resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: sku
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource webAppFull 'Microsoft.Web/sites@2022-03-01' = {
  name: webAppFullName
  location: location
  kind: 'app,linux,container'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOCKER|mcr.microsoft.com/dotnet/aspnet:8.0'
      alwaysOn: true
      appSettings: [
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        {
          name: 'DOCKER_ENABLE_CI'
          value: 'true'
        }
      ]
    }
    httpsOnly: true
  }
}

resource agentAppFull 'Microsoft.Web/sites@2022-03-01' = {
  name: agentAppFullName
  location: location
  kind: 'app,linux,container'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOCKER|mcr.microsoft.com/dotnet/runtime:8.0'
      alwaysOn: true
      appSettings: [
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        {
          name: 'DOCKER_ENABLE_CI'
          value: 'true'
        }
        {
          name: 'OPENAI_API_KEY'
          value: openAiApiKey
        }
        {
          name: 'OpenAI__ModelId'
          value: 'gpt-4o-mini'
        }
      ]
    }
    httpsOnly: true
  }
}

output webAppUrl string = 'https://${webAppFull.properties.defaultHostName}'
output agentAppUrl string = 'https://${agentAppFull.properties.defaultHostName}'
