# Azure Deployment Quickstart Guide

This guide walks you through deploying ALAN to Azure Container Apps using the provided Bicep templates.

## Prerequisites

Before you begin, ensure you have:

1. **Azure Subscription** with appropriate permissions:
   - Create resource groups and resources
   - Assign role-based access control (RBAC) roles
   - Register resource providers

2. **Development Tools:**
   - [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) version 2.50.0 or later
   - [Azure Developer CLI (azd)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) (recommended)
   - [Docker](https://docs.docker.com/get-docker/) for building container images
   - [Git](https://git-scm.com/) for cloning the repository

3. **Azure Resources:**
   - Azure OpenAI access (may require application)
   - Sufficient quota for Container Apps and other resources in your region

## Step 1: Clone the Repository

```bash
git clone https://github.com/jmservera/ALAN.git
cd ALAN
```

## Step 2: Login to Azure

```bash
# Login to Azure
az login

# Set your subscription (if you have multiple)
az account set --subscription "Your Subscription Name"

# Verify you're logged in
az account show
```

## Step 3: Choose Your Deployment Method

### Option A: Using Azure Developer CLI (Recommended)

The Azure Developer CLI (azd) provides the simplest deployment experience.

#### 3.1 Initialize the Environment

```bash
# Initialize azd (if first time)
azd init

# This creates an .azure directory for environment configuration
```

#### 3.2 Configure Environment Variables

```bash
# Set required parameters
azd env set AZURE_ENV_NAME dev
azd env set AZURE_LOCATION eastus

# Get your user ID for role assignments
azd env set AZURE_PRINCIPAL_ID $(az ad signed-in-user show --query id -o tsv)

# Optional: Configure OpenAI settings
azd env set AZURE_OPENAI_DEPLOYMENT gpt-4o-mini
azd env set AZURE_OPENAI_MODEL_NAME gpt-4o-mini

# Optional: Configure reliability features
azd env set ENABLE_ZONE_REDUNDANCY false  # Set to true for production
azd env set ENABLE_AUTO_SCALING false     # Set to true for production
```

#### 3.3 Provision Infrastructure

```bash
# Deploy all infrastructure
azd provision

# This will:
# - Create resource group
# - Deploy VNet, Storage, OpenAI, Container Registry
# - Create Container Apps Environment
# - Set up managed identity and role assignments
# - Configure private endpoints and DNS
```

The provisioning process takes approximately 10-15 minutes.

#### 3.4 Build and Push Container Images

After infrastructure is provisioned, get the container registry name:

```bash
# Get registry name from outputs
REGISTRY_NAME=$(azd env get-values | grep AZURE_CONTAINER_REGISTRY_NAME | cut -d= -f2 | tr -d '"')
echo "Container Registry: $REGISTRY_NAME"

# Login to ACR
az acr login --name $REGISTRY_NAME

# Build and push images using ACR build tasks (recommended)
az acr build --registry $REGISTRY_NAME --image alan-agent:latest -f Dockerfile.agent .
az acr build --registry $REGISTRY_NAME --image alan-chatapi:latest -f Dockerfile.chatapi .
az acr build --registry $REGISTRY_NAME --image alan-web:latest -f Dockerfile.web .
```

#### 3.5 Deploy Applications

```bash
# Deploy container apps
azd deploy

# Or manually update Container Apps to use new images
az containerapp update \
  --name ca-agent-dev \
  --resource-group rg-dev \
  --image $REGISTRY_NAME.azurecr.io/alan-agent:latest

az containerapp update \
  --name ca-chatapi-dev \
  --resource-group rg-dev \
  --image $REGISTRY_NAME.azurecr.io/alan-chatapi:latest

az containerapp update \
  --name ca-web-dev \
  --resource-group rg-dev \
  --image $REGISTRY_NAME.azurecr.io/alan-web:latest
```

#### 3.6 Access Your Application

```bash
# Get the web application URL
WEB_URL=$(azd env get-values | grep WEB_APP_URL | cut -d= -f2 | tr -d '"')
echo "Your ALAN application is available at: $WEB_URL"

# Open in browser
open $WEB_URL  # macOS
# or
xdg-open $WEB_URL  # Linux
# or
start $WEB_URL  # Windows
```

### Option B: Using Azure CLI

If you prefer not to use azd, you can deploy directly with Azure CLI.

#### 3.1 Set Variables

```bash
# Set deployment parameters
ENVIRONMENT_NAME="dev"
LOCATION="eastus"
RESOURCE_GROUP="rg-alan-${ENVIRONMENT_NAME}"
PRINCIPAL_ID=$(az ad signed-in-user show --query id -o tsv)
```

#### 3.2 Create Resource Group

```bash
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION
```

#### 3.3 Deploy Infrastructure

```bash
# Deploy using subscription-level deployment
az deployment sub create \
  --location $LOCATION \
  --template-file ./infra/main.bicep \
  --parameters ./infra/main.parameters.json \
  --parameters environmentName=$ENVIRONMENT_NAME \
  --parameters location=$LOCATION \
  --parameters principalId=$PRINCIPAL_ID \
  --parameters principalType=User
```

#### 3.4 Get Deployment Outputs

```bash
# Get outputs
az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name resources-${ENVIRONMENT_NAME} \
  --query properties.outputs

# Get specific values
REGISTRY_NAME=$(az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name resources-${ENVIRONMENT_NAME} \
  --query properties.outputs.AZURE_CONTAINER_REGISTRY_NAME.value -o tsv)

WEB_URL=$(az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name resources-${ENVIRONMENT_NAME} \
  --query properties.outputs.WEB_APP_URL.value -o tsv)
```

#### 3.5 Build and Deploy Container Images

Follow the same steps as in Option A (3.4) to build and push images, then update Container Apps.

## Step 4: Configure Local Development Environment

After deployment, update your local `.env` file with the deployed infrastructure values:

```bash
# Export outputs to .env file (if using azd)
azd env get-values > .env.azure

# Or manually get values
az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name resources-${ENVIRONMENT_NAME} \
  --query properties.outputs -o json
```

Update your `.env` file with these values:
```env
# From deployment outputs
AZURE_OPENAI_ENDPOINT="https://cog-alan-dev-abc123.openai.azure.com/"
AZURE_OPENAI_DEPLOYMENT="gpt-4o-mini"
AZURE_STORAGE_ACCOUNT_NAME="stabc123"
AZURE_CLIENT_ID="00000000-0000-0000-0000-000000000000"

# Your Azure credentials
AZURE_TENANT_ID="your-tenant-id"
AZURE_SUBSCRIPTION_ID="your-subscription-id"
```

## Step 5: Verify Deployment

### Check Container App Status

```bash
# List all container apps
az containerapp list \
  --resource-group $RESOURCE_GROUP \
  --output table

# Check specific app status
az containerapp show \
  --name ca-web-dev \
  --resource-group $RESOURCE_GROUP \
  --query properties.runningStatus
```

### View Logs

```bash
# View agent logs
az containerapp logs show \
  --name ca-agent-dev \
  --resource-group $RESOURCE_GROUP \
  --follow

# View chatapi logs
az containerapp logs show \
  --name ca-chatapi-dev \
  --resource-group $RESOURCE_GROUP \
  --follow

# View web logs
az containerapp logs show \
  --name ca-web-dev \
  --resource-group $RESOURCE_GROUP \
  --follow
```

### Test the Application

```bash
# Access the web UI
curl -I $WEB_URL

# Test the ChatAPI endpoint (internal - requires VNet access)
# This will fail from outside as it's internal only
curl https://ca-chatapi-dev.internal.example.azurecontainerapps.io/api/state
```

## Step 6: Monitor and Troubleshoot

### View Metrics in Azure Portal

1. Navigate to Azure Portal: https://portal.azure.com
2. Search for your resource group (e.g., `rg-alan-dev`)
3. Click on Container Apps Environment
4. View metrics, logs, and health status

### Query Logs in Log Analytics

```bash
# Get Log Analytics workspace ID
WORKSPACE_ID=$(az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name resources-${ENVIRONMENT_NAME} \
  --query properties.outputs.logAnalyticsWorkspaceId.value -o tsv)

# Query logs using KQL
az monitor log-analytics query \
  --workspace $WORKSPACE_ID \
  --analytics-query "ContainerAppConsoleLogs_CL | where ContainerAppName_s == 'ca-agent-dev' | order by TimeGenerated desc | take 50"
```

### Common Issues

**Container App shows "Provisioning Failed":**
- Check managed identity has correct role assignments
- Verify container images exist in ACR
- Review Container App logs for specific errors

**Cannot access web application:**
- Verify ingress is enabled and external
- Check if Container Apps Environment is healthy
- Ensure DNS is resolving correctly

**Storage or OpenAI access denied:**
- Verify managed identity is assigned to Container Apps
- Check role assignments on Storage Account and OpenAI
- Ensure AZURE_CLIENT_ID environment variable is set

## Step 7: Production Deployment

For production deployments, enable reliability features:

```bash
# Set production parameters
azd env set AZURE_ENV_NAME prod
azd env set ENABLE_ZONE_REDUNDANCY true
azd env set ENABLE_AUTO_SCALING true
azd env set MIN_REPLICAS 2
azd env set MAX_REPLICAS 10

# Provision production infrastructure
azd provision

# Deploy applications
azd deploy
```

## Step 8: Clean Up Resources

When you're done testing, clean up resources to avoid charges:

```bash
# Using azd
azd down --purge

# Or using Azure CLI
az group delete --name $RESOURCE_GROUP --yes --no-wait
```

## Next Steps

- **Configure CI/CD**: Set up automated deployments using Azure DevOps or GitHub Actions
- **Enable monitoring**: Configure Application Insights for detailed telemetry
- **Customize configuration**: Adjust OpenAI models, agent parameters, and scaling rules
- **Secure secrets**: Move sensitive configuration to Azure Key Vault
- **Set up alerts**: Configure Azure Monitor alerts for critical issues

## Additional Resources

- [Infrastructure README](../infra/README.md) - Detailed infrastructure documentation
- [Azure Container Apps Documentation](https://learn.microsoft.com/azure/container-apps/)
- [Azure Verified Modules](https://azure.github.io/Azure-Verified-Modules/)
- [Azure Developer CLI Documentation](https://learn.microsoft.com/azure/developer/azure-developer-cli/)

## Support

For issues or questions:
- Check [GitHub Issues](https://github.com/jmservera/ALAN/issues)
- Review logs in Azure Portal
- Consult Azure Container Apps documentation
