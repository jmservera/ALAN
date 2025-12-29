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

4. **Azure AI Search** (Private)
   - Vector search for semantic memory retrieval
   - Basic SKU (configurable to Standard/Premium)
   - Private endpoint for secure access
   - Managed identity authentication

5. **Container Registry** (Private)
   - Stores container images for agent, chatapi, and web
   - Managed identity for pull access

6. **Container Apps Environment**
   - Integrated with VNet for security
   - Connected to Log Analytics for monitoring
   - Optional zone redundancy for production

7. **Container Apps** (3 applications)
   - **alan-agent**: Background agent service (internal only)
   - **alan-chatapi**: API service (internal only)
   - **alan-web**: Web UI (public ingress)

7. **Managed Identity**
   - User-assigned identity for all applications
   - Permissions for Storage, OpenAI, AI Search, and Container Registry

8. **Log Analytics Workspace**
   - Centralized logging and monitoring
   - 30-day retention

### Security Features

- **Private Endpoints**: All Azure services (Storage, OpenAI, AI Search, Container Registry) are accessible only through private endpoints
- **Network Isolation**: Resources deployed in VNet with controlled access
- **Managed Identity**: No connection strings or keys stored in configuration
- **Public Access**: Only the web application has a public endpoint
- **Private DNS Zones**: Automatic DNS resolution for private endpoints
- **IP Allowlisting**: Automatically whitelists your current IP address for Azure services during deployment
- **Role-Based Access**: Automatically assigns your user identity appropriate roles for accessing resources

### Automatic User Identity and IP Retrieval

When using `azd provision`, the infrastructure **automatically**:

1. **Retrieves your Azure AD user identity** and assigns it to `AZURE_PRINCIPAL_ID`
2. **Detects your current public IP address** and assigns it to `AZURE_USER_IP`
3. **Adds your IP to allowlists** for Storage Account, Azure OpenAI, and Container Registry
4. **Grants your user identity** appropriate roles (Storage Contributor, OpenAI User, etc.)

This allows you to access the deployed resources directly from your local machine for testing and management, while maintaining security through private endpoints for production traffic.

The IP allowlisting enables:

- Direct access to Storage Account from Azure Portal and local tools
- Testing Azure OpenAI endpoints from your development machine
- Pushing/pulling container images from local Docker
- Access persists across `azd provision` runs and updates automatically

To manually override these values:

```bash
azd env set AZURE_PRINCIPAL_ID "your-user-object-id"
azd env set AZURE_USER_IP "your-public-ip-address"
```

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

The `azd` deployment automatically retrieves your user identity and public IP address during the preprovision phase.

1. **Initialize the environment:**

   ```bash
   azd init
   ```

2. **Set required environment variables:**

   ```bash
   azd env set AZURE_ENV_NAME dev
   azd env set AZURE_LOCATION eastus
   ```

   > **Note**: You don't need to manually set `AZURE_PRINCIPAL_ID` or `AZURE_USER_IP` - these are automatically detected during provisioning!

3. **Provision infrastructure:**

   ```bash
   azd provision
   ```

   During provisioning, you'll see output like:

   ```
   Retrieving current user identity...
   User Principal ID: 12345678-1234-1234-1234-123456789abc
   Retrieving current user's public IP...
   User IP Address: 203.0.113.42
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

| Parameter         | Description             | Example                  |
| ----------------- | ----------------------- | ------------------------ |
| `environmentName` | Name of the environment | `dev`, `staging`, `prod` |
| `location`        | Azure region            | `eastus`, `westus2`      |

### Optional Parameters

| Parameter              | Default         | Description                                                  |
| ---------------------- | --------------- | ------------------------------------------------------------ |
| `principalId`          | (auto-detected) | Your Azure AD user/service principal ID for role assignments |
| `principalType`        | `User`          | Type of principal: `User`, `ServicePrincipal`, or `Group`    |
| `userIpAddress`        | (auto-detected) | Your public IP address to whitelist for Azure services       |
| `openAiDeploymentName` | `gpt-4o-mini`   | Name for the OpenAI deployment                               |
| `openAiModelName`      | `gpt-4o-mini`   | OpenAI model to deploy                                       |
| `openAiModelVersion`   | `2024-07-18`    | Model version                                                |
| `openAiModelCapacity`  | `100`           | Capacity in thousands of tokens per minute                   |
| `searchServiceSku`     | `basic`         | AI Search SKU: `basic`, `standard`, `standard2`, `standard3` |
| `searchServiceReplicaCount` | `1`        | Number of replicas for AI Search (1-12)                      |
| `searchServicePartitionCount` | `1`      | Number of partitions for AI Search (1, 2, 3, 4, 6, or 12)   |
| `agentMaxLoopsPerDay`  | `4000`          | Maximum agent loops per day                                  |
| `agentMaxTokensPerDay` | `8000000`       | Maximum tokens per day                                       |
| `agentThinkInterval`   | `5`             | Seconds between agent thoughts                               |
| `enableZoneRedundancy` | `false`         | Enable zone redundancy (production)                          |
| `enableAutoScaling`    | `false`         | Enable auto-scaling for Container Apps                       |
| `minReplicas`          | `1`             | Minimum replica count                                        |
| `maxReplicas`          | `10`            | Maximum replica count (when auto-scaling)                    |

## Outputs

After deployment, the following outputs are available for local development:

| Output                             | Description                | Usage                 |
| ---------------------------------- | -------------------------- | --------------------- |
| `AZURE_OPENAI_ENDPOINT`            | OpenAI endpoint URL        | Set in `.env`         |
| `AZURE_OPENAI_DEPLOYMENT`          | Deployment name            | Set in `.env`         |
| `AZURE_AI_SEARCH_ENDPOINT`         | AI Search endpoint URL     | Set in `.env`         |
| `AZURE_AI_SEARCH_NAME`             | AI Search service name     | For management        |
| `AZURE_STORAGE_ACCOUNT_NAME`       | Storage account name       | For connection string |
| `AZURE_STORAGE_CONNECTION_STRING`  | Full connection string     | Set in `.env`         |
| `WEB_APP_URL`                      | Public web application URL | Access the UI         |
| `CHATAPI_URL`                      | Internal ChatAPI URL       | For testing           |
| `AZURE_MANAGED_IDENTITY_CLIENT_ID` | Managed identity client ID | For local testing     |

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

### Understanding the Placeholder Image Strategy

**Important**: The infrastructure uses a **placeholder image strategy** to solve the chicken-and-egg problem of deploying Container Apps before images exist in the registry.

#### How It Works

1. **Initial Provision**: Container Apps are created with Microsoft's public `containerapps-helloworld` image as a placeholder
2. **Build & Push**: After infrastructure is ready, `azd package` builds and pushes your actual images to the Container Registry
3. **Update**: Container Apps are automatically updated to use your real images

This is handled automatically by `azd up` or can be done manually with `azd provision` followed by `azd deploy`.

#### The Deployment Flow

```bash
# Option 1: All-in-one (recommended)
azd up
# ├── Provisions with placeholder images
# ├── Builds and pushes real images
# └── Updates Container Apps with real images

# Option 2: Step-by-step
azd provision  # Creates infrastructure with placeholders
azd package    # Builds and pushes real images
azd deploy     # Updates Container Apps with real images
```

For detailed information about this strategy, see [Container Image Deployment Strategy](../docs/CONTAINER_IMAGE_DEPLOYMENT.md).

### Manual Image Build and Push

If you need to manually build and push images (without azd):

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

# Update Container Apps to use the new images
az containerapp update --name ca-agent-dev --resource-group rg-alan-dev \
  --image <registry-name>.azurecr.io/alan-agent:latest

az containerapp update --name ca-chatapi-dev --resource-group rg-alan-dev \
  --image <registry-name>.azurecr.io/alan-chatapi:latest

az containerapp update --name ca-web-dev --resource-group rg-alan-dev \
  --image <registry-name>.azurecr.io/alan-web:latest
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

### Container App Deployment Timeout During `azd up`

**Symptom**: `ContainerAppOperationError: Failed to provision revision... Operation expired.`

**Cause**: This typically happens when the infrastructure tries to use container images that don't exist yet.

**Solution**: This is now handled automatically by the placeholder image strategy! If you still encounter this:

1. Ensure you're using the latest version of the templates
2. Check that environment variables are set: `azd env get-values | grep IMAGE`
3. Verify the placeholder image is accessible: `mcr.microsoft.com/azuredocs/containerapps-helloworld:latest`
4. Check Container Apps Environment network connectivity to Microsoft Container Registry

### Container App Not Starting

1. Check logs in Azure Portal
2. Verify managed identity has correct permissions
3. Check which image is being used: `az containerapp show --name <app-name> --resource-group <rg> --query properties.template.containers[0].image`
4. If still using placeholder image, manually update:
   ```bash
   az containerapp update --name <app-name> --resource-group <rg> --image <registry>.azurecr.io/<image>:latest
   ```
5. Verify environment variables are set correctly

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

### Security Scanning

ALAN includes automated security scanning with Checkov to validate infrastructure templates:

**Run security scan locally:**

```bash
./scripts/security-check.sh
```

**Configuration:**

- Security rules are configured in `.checkov.yml` at the repository root
- Checkov scans all Bicep templates for Azure security best practices
- Integrated into CI/CD pipeline with GitHub Actions

**CI/CD Integration:**

- GitHub Actions: `.github/workflows/security-scan.yml` runs on every push/PR to infrastructure files

**What Checkov validates:**

- Network security configurations
- Storage encryption and access controls
- Identity and access management
- Resource configuration best practices
- Compliance with Azure security standards

## Further Reading

- [Azure Container Apps Documentation](https://learn.microsoft.com/azure/container-apps/)
- [Azure Verified Modules](https://azure.github.io/Azure-Verified-Modules/)
- [Azure OpenAI Service](https://learn.microsoft.com/azure/ai-services/openai/)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/)
- [Checkov Documentation](https://www.checkov.io/)
