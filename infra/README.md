# ALAN Infrastructure

This directory contains the Bicep templates for deploying ALAN (Autonomous Learning Agent Network) to Azure using Container Apps.

## Architecture Overview

The infrastructure deploys a complete Azure environment with the following components:

### Core Resources

1. **Virtual Network** - Private network with three subnets:
   - Infrastructure subnet for general resources
   - Private endpoint subnet for secure connectivity
   - Container Apps subnet for the application workloads

2. **Azure Storage Account** (Private)
   - Blob containers: `agent-state`, `short-term-memory`, `long-term-memory`
   - Queue: `human-inputs`
   - Private endpoints for blob and queue services
   - Managed identity authentication

3. **Azure OpenAI** (Private)
   - GPT-4o-mini deployment (configurable)
   - Private endpoint for secure access
   - Managed identity authentication

4. **Container Registry** (Private)
   - Stores container images for agent, chatapi, and web
   - Managed identity for pull access

5. **Container Apps Environment**
   - Integrated with VNet for security
   - Connected to Log Analytics for monitoring
   - Optional zone redundancy for production

6. **Container Apps** (3 applications)
   - **alan-agent**: Background agent service (internal only)
   - **alan-chatapi**: API service (internal only)
   - **alan-web**: Web UI (public ingress)

7. **Managed Identity**
   - User-assigned identity for all applications
   - Permissions for Storage, OpenAI, and Container Registry

8. **Log Analytics Workspace**
   - Centralized logging and monitoring
   - 30-day retention

### Security Features

- **Private Endpoints**: All Azure services (Storage, OpenAI) are accessible only through private endpoints
- **Network Isolation**: Resources deployed in VNet with controlled access
- **Managed Identity**: No connection strings or keys stored in configuration
- **Public Access**: Only the web application has a public endpoint
- **Private DNS Zones**: Automatic DNS resolution for private endpoints

### Optional Reliability Features

- **Zone Redundancy**: Enable for production workloads (`enableZoneRedundancy=true`)
- **Auto-scaling**: Container Apps can scale based on HTTP load (`enableAutoScaling=true`)
- **Multiple Replicas**: Configure min/max replicas for high availability

## Prerequisites

- Azure CLI (`az`) version 2.50.0 or later
- Azure Developer CLI (`azd`) (optional but recommended)
- An Azure subscription
- Permissions to create resources and assign roles

## Deployment

### Using Azure Developer CLI (azd) - Recommended

1. **Initialize the environment:**
   ```bash
   azd init
   ```

2. **Set environment variables** (or use `.env` file):
   ```bash
   azd env set AZURE_ENV_NAME dev
   azd env set AZURE_LOCATION eastus
   azd env set AZURE_PRINCIPAL_ID $(az ad signed-in-user show --query id -o tsv)
   ```

3. **Provision infrastructure:**
   ```bash
   azd provision
   ```

4. **Deploy applications** (after building container images):
   ```bash
   azd deploy
   ```

### Using Azure CLI

1. **Create a resource group:**
   ```bash
   az group create --name rg-alan-dev --location eastus
   ```

2. **Deploy the infrastructure:**
   ```bash
   az deployment group create \
     --resource-group rg-alan-dev \
     --template-file ./infra/resources.bicep \
     --parameters @./infra/main.parameters.json \
     --parameters principalId=$(az ad signed-in-user show --query id -o tsv)
   ```

3. **Get outputs:**
   ```bash
   az deployment group show \
     --resource-group rg-alan-dev \
     --name resources-dev \
     --query properties.outputs
   ```

## Configuration Parameters

### Required Parameters

| Parameter | Description | Example |
|-----------|-------------|---------|
| `environmentName` | Name of the environment | `dev`, `staging`, `prod` |
| `location` | Azure region | `eastus`, `westus2` |

### Optional Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `principalId` | (empty) | Your Azure AD user/service principal ID for role assignments |
| `principalType` | `User` | Type of principal: `User`, `ServicePrincipal`, or `Group` |
| `openAiDeploymentName` | `gpt-4o-mini` | Name for the OpenAI deployment |
| `openAiModelName` | `gpt-4o-mini` | OpenAI model to deploy |
| `openAiModelVersion` | `2024-07-18` | Model version |
| `openAiModelCapacity` | `100` | Capacity in thousands of tokens per minute |
| `agentMaxLoopsPerDay` | `4000` | Maximum agent loops per day |
| `agentMaxTokensPerDay` | `8000000` | Maximum tokens per day |
| `agentThinkInterval` | `5` | Seconds between agent thoughts |
| `enableZoneRedundancy` | `false` | Enable zone redundancy (production) |
| `enableAutoScaling` | `false` | Enable auto-scaling for Container Apps |
| `minReplicas` | `1` | Minimum replica count |
| `maxReplicas` | `10` | Maximum replica count (when auto-scaling) |

## Outputs

After deployment, the following outputs are available for local development:

| Output | Description | Usage |
|--------|-------------|-------|
| `AZURE_OPENAI_ENDPOINT` | OpenAI endpoint URL | Set in `.env` |
| `AZURE_OPENAI_DEPLOYMENT` | Deployment name | Set in `.env` |
| `AZURE_STORAGE_ACCOUNT_NAME` | Storage account name | For connection string |
| `AZURE_STORAGE_CONNECTION_STRING` | Full connection string | Set in `.env` |
| `WEB_APP_URL` | Public web application URL | Access the UI |
| `CHATAPI_URL` | Internal ChatAPI URL | For testing |
| `AZURE_MANAGED_IDENTITY_CLIENT_ID` | Managed identity client ID | For local testing |

### Using Outputs for Local Development

After deployment, update your `.env` file with the output values:

```bash
# Get all outputs
azd env get-values

# Or using Azure CLI
az deployment group show \
  --resource-group rg-alan-dev \
  --name resources-dev \
  --query properties.outputs -o json
```

Example `.env` configuration from outputs:
```bash
AZURE_OPENAI_ENDPOINT="https://cog-alan-dev-abc123.openai.azure.com/"
AZURE_OPENAI_DEPLOYMENT="gpt-4o-mini"
AZURE_STORAGE_ACCOUNT_NAME="stabc123"
AZURE_CLIENT_ID="00000000-0000-0000-0000-000000000000"
```

## File Structure

```
infra/
├── main.bicep                      # Main entry point (subscription scope)
├── main.parameters.json            # Parameters file with environment variable support
├── resources.bicep                 # Main resource deployment (resource group scope)
├── abbreviations.json              # Azure resource naming abbreviations
├── modules/
│   └── container-app.bicep        # Reusable Container App module
└── README.md                       # This file
```

## Azure Verified Modules (AVM)

This infrastructure uses Azure Verified Modules for the following resources:
- Managed Identity
- Log Analytics Workspace
- Virtual Network
- Storage Account
- Private DNS Zones
- Cognitive Services (OpenAI)
- Container Registry
- Container Apps Environment

These modules follow Microsoft best practices and are maintained by the Azure team.

## Development vs Production

### Development Configuration
```bash
azd env set ENABLE_ZONE_REDUNDANCY false
azd env set ENABLE_AUTO_SCALING false
azd env set MIN_REPLICAS 1
```

### Production Configuration
```bash
azd env set ENABLE_ZONE_REDUNDANCY true
azd env set ENABLE_AUTO_SCALING true
azd env set MIN_REPLICAS 2
azd env set MAX_REPLICAS 10
```

## Building and Pushing Container Images

Before deploying the Container Apps, build and push the Docker images:

```bash
# Login to Azure Container Registry
az acr login --name <registry-name>

# Build and push images
docker build -f Dockerfile.agent -t <registry-name>.azurecr.io/alan-agent:latest .
docker push <registry-name>.azurecr.io/alan-agent:latest

docker build -f Dockerfile.chatapi -t <registry-name>.azurecr.io/alan-chatapi:latest .
docker push <registry-name>.azurecr.io/alan-chatapi:latest

docker build -f Dockerfile.web -t <registry-name>.azurecr.io/alan-web:latest .
docker push <registry-name>.azurecr.io/alan-web:latest
```

Or use the Azure Container Registry build tasks:

```bash
az acr build --registry <registry-name> --image alan-agent:latest -f Dockerfile.agent .
az acr build --registry <registry-name> --image alan-chatapi:latest -f Dockerfile.chatapi .
az acr build --registry <registry-name> --image alan-web:latest -f Dockerfile.web .
```

## Monitoring

Access logs and metrics through:
- **Azure Portal**: Navigate to Container Apps → Logs
- **Log Analytics**: Query logs using KQL
- **Application Insights**: Enable for detailed telemetry (optional)

## Troubleshooting

### Container App Not Starting

1. Check logs in Azure Portal
2. Verify managed identity has correct permissions
3. Ensure container images are pushed to registry
4. Verify environment variables are set correctly

### Network Connectivity Issues

1. Verify private endpoints are provisioned
2. Check DNS resolution in private DNS zones
3. Ensure VNet integration is configured correctly

### Authentication Issues

1. Verify managed identity is assigned to Container Apps
2. Check role assignments on Storage and OpenAI
3. Ensure AZURE_CLIENT_ID environment variable is set

## Cost Estimation

Approximate monthly costs for a development environment:
- Container Apps Environment: ~$0
- Container Apps (3 apps, 0.5 vCPU, 1GB each): ~$30-50
- Storage Account (LRS): ~$5-10
- Azure OpenAI (gpt-4o-mini, 100K TPM): ~$50-200 (usage-based)
- Virtual Network: ~$0
- Log Analytics (30-day retention): ~$10-20
- Container Registry (Basic): ~$5

**Total estimated cost: $100-300/month** (varies with usage)

Production with zone redundancy and auto-scaling will cost more.

## Security Best Practices

1. **Never commit secrets**: Use managed identity or Key Vault
2. **Limit public access**: Only web app should be public
3. **Enable monitoring**: Use Log Analytics for security auditing
4. **Regular updates**: Keep container images updated
5. **Network isolation**: Use private endpoints for all services
6. **Least privilege**: Assign minimum required permissions

## Further Reading

- [Azure Container Apps Documentation](https://learn.microsoft.com/azure/container-apps/)
- [Azure Verified Modules](https://azure.github.io/Azure-Verified-Modules/)
- [Azure OpenAI Service](https://learn.microsoft.com/azure/ai-services/openai/)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/)
