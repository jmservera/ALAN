// Reusable Container App module for ALAN components

@description('Name of the container app')
param name string

@description('Location for the container app')
param location string

@description('Tags to apply to the container app')
param tags object

@description('Resource ID of the Container Apps Environment')
param containerAppsEnvironmentId string

@description('Resource ID of the managed identity')
param managedIdentityId string

@description('Name of the container registry')
param containerRegistryName string

@description('Container image to deploy (name:tag)')
param containerImage string

@description('Container port to expose (0 for no port)')
param containerPort int

@description('Enable ingress for the container app')
param enableIngress bool

@description('Enable external ingress (public access)')
param ingressExternal bool = false

@description('Environment variables for the container')
param environmentVariables array

@description('Minimum number of replicas')
param minReplicas int

@description('Maximum number of replicas')
param maxReplicas int

@description('CPU allocation (e.g., "0.5", "1.0")')
param cpu string

@description('Memory allocation (e.g., "1Gi", "2Gi")')
param memory string

@description('Scaling rule type: http, cpu, or none')
@allowed([
  'http'
  'cpu'
  'none'
])
param scalingRuleType string = 'http'

@description('Workload profile name (e.g., Consumption for consumption profile)')
param workloadProfileName string = 'Consumption'

// ==================================
// Resources
// ==================================

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironmentId
    workloadProfileName: workloadProfileName
    configuration: {
      ingress: enableIngress ? {
        external: ingressExternal
        targetPort: containerPort
        transport: 'http'
        allowInsecure: false
      } : null
      registries: [
        {
          server: '${containerRegistryName}.azurecr.io'
          identity: managedIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: name
          // Support both ACR images and external images (e.g., mcr.microsoft.com for placeholders)
          // If containerImage starts with a registry domain, use it as-is
          // Otherwise, prefix with the ACR name
          image: contains(containerImage, '/') && !startsWith(containerImage, '${containerRegistryName}.azurecr.io')
            ? containerImage
            : '${containerRegistryName}.azurecr.io/${containerImage}'
          env: environmentVariables
          resources: {
            cpu: json(cpu)
            memory: memory
          }
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: maxReplicas > minReplicas && scalingRuleType != 'none' ? [
          scalingRuleType == 'http' ? {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          } : {
            name: 'cpu-scaling'
            custom: {
              type: 'cpu'
              metadata: {
                type: 'Utilization'
                value: '75'
              }
            }
          }
        ] : []
      }
    }
  }
}

// ==================================
// Outputs
// ==================================

output id string = containerApp.id
output name string = containerApp.name
output fqdn string = enableIngress ? containerApp.properties.configuration.ingress.fqdn : ''
output latestRevisionName string = containerApp.properties.latestRevisionName
