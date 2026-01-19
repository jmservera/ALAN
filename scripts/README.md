# ALAN Deployment Scripts

This directory contains helper scripts for deploying and managing ALAN infrastructure.

## Scripts

### `postprovision.sh` / `postprovision.ps1` ⭐ NEW

Post-provision scripts that enable local development access to Azure OpenAI and Container Registry.

**Features:**

- ✓ Automatically configures OpenAI and ACR access for local development
- ✓ Adds current user's IP to OpenAI network rules
- ✓ Adds current user's IP to Container Registry network rules
- ✓ Assigns Cognitive Services OpenAI User role to current user
- ✓ Assigns AcrPush role to current user for Container Registry
- ✓ Only runs locally (skips when CI environment variable is set)
- ✓ Keeps production deployments fully secured

**Usage:**

These scripts are automatically invoked by `azd provision` via the `postprovision` hook in `azure.yaml`.

**Manual Execution:**

```bash
# Linux/macOS
./scripts/postprovision.sh

# Windows
.\scripts\postprovision.ps1
```

**What it Does:**

1. Checks if running in CI environment (exits if CI is set)
2. Retrieves deployment information from azd environment
3. Gets current user's public IP address
4. Enables public network access on OpenAI resource
5. Adds current IP to OpenAI network rules
6. Adds current IP to Container Registry network rules
7. Assigns Cognitive Services OpenAI User role to current user
8. Assigns AcrPush role to current user for Container Registry

**Requirements:**

- Azure CLI installed and authenticated
- azd environment configured (automatically available after `azd provision`)
- `dig` or `curl` for IP address retrieval

**Security Notes:**

- This script only runs during local development (`CI` environment variable not set)
- Production deployments via GitHub Actions remain fully secured with private endpoints
- Network rules are restrictive - only the current IP is allowed
- Role assignments follow principle of least privilege

### `validate-deployment.sh` ⭐ NEW

A validation script that checks if the container image deployment strategy is correctly configured.

**Features:**

- ✓ Validates Azure CLI and azd installation
- ✓ Checks environment configuration
- ✓ Verifies Bicep template parameters for container images
- ✓ Validates azure.yaml hooks configuration
- ✓ Checks Dockerfile existence
- ✓ Verifies placeholder image accessibility

**Usage:**

```bash
# Run validation before deployment
./scripts/validate-deployment.sh
```

The script will:

1. Check all prerequisites are installed
2. Verify infrastructure files are present
3. Validate container image parameters in Bicep templates
4. Check azure.yaml preprovision/postprovision hooks
5. Confirm Dockerfiles exist
6. Test placeholder image accessibility

**When to Use:**

- Before running `azd up` for the first time
- After modifying infrastructure templates
- When troubleshooting deployment issues
- As part of CI/CD pre-deployment checks

**Exit Codes:**

- `0` - All validations passed
- `1` - Validation failed (see error messages)

### `security-check.sh`

A security scanning script that runs Checkov on Bicep templates to detect security issues.

**Features:**

- ✓ Scans all Bicep templates in `infra/` directory
- ✓ Uses Checkov security scanning tool
- ✓ Checks for Azure security best practices
- ✓ Outputs results in CLI format
- ✓ Integrates with CI/CD pipelines

**Usage:**

```bash
# Make executable (first time only)
chmod +x scripts/security-check.sh

# Run security scan
./scripts/security-check.sh
```

The script will:

1. Check if Checkov is installed
2. Scan all Bicep templates in the `infra/` directory
3. Report any security findings
4. Exit with appropriate status code for CI/CD

**Requirements:**

- Python 3.x
- Checkov installed: `pip install checkov`

**Configuration:**

- Security scanning rules are configured in `.checkov.yml`
- To skip specific checks, add them to the configuration file

### `deploy-azure.sh`

An interactive bash script that simplifies the Azure deployment process.

**Features:**

- ✓ Prerequisites checking (Azure CLI, Docker, azd)
- ✓ Interactive configuration
- ✓ Infrastructure deployment using Bicep templates
- ✓ Container image building and pushing to ACR
- ✓ Container Apps updates
- ✓ Deployment outputs saved to `.env.{environment}.azure`

**Usage:**

```bash
# Make executable (first time only)
chmod +x scripts/deploy-azure.sh

# Run the deployment script
./scripts/deploy-azure.sh
```

The script will:

1. Check prerequisites (Azure CLI, login status, Docker)
2. Prompt for environment name (dev/staging/prod) and location
3. Deploy infrastructure using Bicep templates
4. Build and push container images to Azure Container Registry
5. Update Container Apps with new images
6. Save deployment outputs to an environment file

**Requirements:**

- Azure CLI (`az`) installed and configured
- Logged in to Azure (`az login`)
- Appropriate Azure subscription permissions
- Docker (optional, for building images)

**Output:**

After successful deployment, you'll get:

- A `.env.{environment}.azure` file with deployment outputs
- Web application URL
- Container Registry name
- Storage account name
- Azure OpenAI endpoint

**Example Session:**

```
╔═══════════════════════════════════════╗
║   ALAN Azure Deployment Helper        ║
║   Autonomous Learning Agent Network   ║
╚═══════════════════════════════════════╝

================================
Checking Prerequisites
================================

✓ Azure CLI found: 2.50.0
✓ Logged in to Azure subscription: My Subscription
✓ Docker found: Docker version 24.0.0
✓ Azure Developer CLI found: 1.5.0

================================
Configuration
================================

Environment name (dev/staging/prod) [dev]: dev
ℹ Environment: dev
Azure location [eastus]: eastus
ℹ Location: eastus
ℹ Principal ID: 12345678-1234-1234-1234-123456789012

Deploy with these settings? (y/n): y

================================
Deploying Infrastructure
================================

ℹ Creating resource group: rg-alan-dev
✓ Resource group created
ℹ Deploying Bicep templates... (this may take 10-15 minutes)
✓ Infrastructure deployed successfully
ℹ Retrieving deployment outputs...
✓ Deployment outputs retrieved

================================
Building Container Images
================================

ℹ Logging in to Azure Container Registry...
ℹ Building and pushing agent image...
✓ Agent image built and pushed
ℹ Building and pushing chatapi image...
✓ ChatApi image built and pushed
ℹ Building and pushing web image...
✓ Web image built and pushed

================================
Updating Container Apps
================================

ℹ Updating agent container app...
ℹ Updating chatapi container app...
ℹ Updating web container app...
✓ Container apps updated

================================
Saving Deployment Outputs
================================

✓ Outputs saved to: .env.dev.azure

================================
Deployment Complete! 🎉
================================

Your ALAN application has been deployed to Azure.

Key Information:
  Environment:        dev
  Resource Group:     rg-alan-dev
  Location:           eastus

Application URLs:
  Web Application:    https://ca-web-dev.example.azurecontainerapps.io

Next Steps:
  1. Visit your web application: https://ca-web-dev.example.azurecontainerapps.io
  2. Check Container Apps status in Azure Portal
  3. View logs: az containerapp logs show --name ca-web-dev --resource-group rg-alan-dev --follow
  4. Update your local .env file with values from: .env.dev.azure

Documentation:
  - Infrastructure details: infra/README.md
  - Deployment guide: docs/AZURE_DEPLOYMENT.md
```

## Manual Deployment

If you prefer manual deployment or need more control, see:

- [Azure Deployment Guide](../docs/AZURE_DEPLOYMENT.md)
- [Infrastructure README](../infra/README.md)

## Troubleshooting

**Script fails with "Azure CLI not found":**

- Install Azure CLI: https://learn.microsoft.com/cli/azure/install-azure-cli

**Script fails with "Not logged in to Azure":**

- Run: `az login`
- Verify: `az account show`

**Container image builds fail:**

- Ensure Docker is running
- Check Docker daemon is accessible
- Try using ACR build tasks instead: `az acr build --registry <name> --image <image> -f <Dockerfile> .`

**Container Apps update fails:**

- Container Apps might not exist yet on first deployment
- Check Container Apps status in Azure Portal
- Verify images were pushed to ACR successfully

## Contributing

When adding new deployment scripts:

1. Follow the same error handling pattern
2. Include helpful output messages with colors
3. Document prerequisites and usage
4. Test on different environments (dev/staging/prod)
5. Update this README
