#!/bin/bash
# Validation script for container image deployment strategy
# Run this before deploying to ensure everything is configured correctly

set -e

echo "==================================="
echo "Container Image Deployment Validator"
echo "==================================="
echo ""

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Check if azd is installed
if ! command -v azd &> /dev/null; then
    echo -e "${RED}✗ Azure Developer CLI (azd) is not installed${NC}"
    echo "  Install from: https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd"
    exit 1
fi
echo -e "${GREEN}✓ Azure Developer CLI (azd) is installed${NC}"

# Check if az is installed
if ! command -v az &> /dev/null; then
    echo -e "${RED}✗ Azure CLI (az) is not installed${NC}"
    echo "  Install from: https://learn.microsoft.com/cli/azure/install-azure-cli"
    exit 1
fi
echo -e "${GREEN}✓ Azure CLI (az) is installed${NC}"

# Check if logged into Azure
if ! az account show &> /dev/null; then
    echo -e "${RED}✗ Not logged into Azure CLI${NC}"
    echo "  Run: az login"
    exit 1
fi
echo -e "${GREEN}✓ Logged into Azure CLI${NC}"

# Check environment variables
echo ""
echo "Checking azd environment..."

ENV_NAME=$(azd env get-values 2>/dev/null | grep AZURE_ENV_NAME | cut -d= -f2 | tr -d '"' || echo "")
if [ -z "$ENV_NAME" ]; then
    echo -e "${YELLOW}! Environment name not set${NC}"
    echo "  Run: azd env set AZURE_ENV_NAME <your-env-name>"
else
    echo -e "${GREEN}✓ Environment name: $ENV_NAME${NC}"
fi

LOCATION=$(azd env get-values 2>/dev/null | grep AZURE_LOCATION | cut -d= -f2 | tr -d '"' || echo "")
if [ -z "$LOCATION" ]; then
    echo -e "${YELLOW}! Location not set${NC}"
    echo "  Run: azd env set AZURE_LOCATION <azure-region>"
else
    echo -e "${GREEN}✓ Location: $LOCATION${NC}"
fi

# Check Bicep files exist
echo ""
echo "Checking infrastructure files..."

if [ ! -f "infra/main.bicep" ]; then
    echo -e "${RED}✗ infra/main.bicep not found${NC}"
    exit 1
fi
echo -e "${GREEN}✓ main.bicep exists${NC}"

if [ ! -f "infra/resources.bicep" ]; then
    echo -e "${RED}✗ infra/resources.bicep not found${NC}"
    exit 1
fi
echo -e "${GREEN}✓ resources.bicep exists${NC}"

if [ ! -f "infra/modules/container-app.bicep" ]; then
    echo -e "${RED}✗ infra/modules/container-app.bicep not found${NC}"
    exit 1
fi
echo -e "${GREEN}✓ container-app.bicep exists${NC}"

if [ ! -f "infra/main.parameters.json" ]; then
    echo -e "${RED}✗ infra/main.parameters.json not found${NC}"
    exit 1
fi
echo -e "${GREEN}✓ main.parameters.json exists${NC}"

# Check for container image parameters in Bicep
echo ""
echo "Validating container image parameters..."

if grep -q "agentContainerImage" infra/main.bicep; then
    echo -e "${GREEN}✓ agentContainerImage parameter found in main.bicep${NC}"
else
    echo -e "${RED}✗ agentContainerImage parameter not found in main.bicep${NC}"
    exit 1
fi

if grep -q "chatApiContainerImage" infra/main.bicep; then
    echo -e "${GREEN}✓ chatApiContainerImage parameter found in main.bicep${NC}"
else
    echo -e "${RED}✗ chatApiContainerImage parameter not found in main.bicep${NC}"
    exit 1
fi

if grep -q "webContainerImage" infra/main.bicep; then
    echo -e "${GREEN}✓ webContainerImage parameter found in main.bicep${NC}"
else
    echo -e "${RED}✗ webContainerImage parameter not found in main.bicep${NC}"
    exit 1
fi

# Check azure.yaml
echo ""
echo "Validating azure.yaml configuration..."

if [ ! -f "azure.yaml" ]; then
    echo -e "${RED}✗ azure.yaml not found${NC}"
    exit 1
fi
echo -e "${GREEN}✓ azure.yaml exists${NC}"

if grep -q "AGENT_CONTAINER_IMAGE" azure.yaml; then
    echo -e "${GREEN}✓ Preprovision hook sets placeholder images${NC}"
else
    echo -e "${YELLOW}! Preprovision hook may not set placeholder images${NC}"
fi

if grep -q "containerapp update" azure.yaml; then
    echo -e "${GREEN}✓ Postprovision hook updates container apps${NC}"
else
    echo -e "${YELLOW}! Postprovision hook may not update container apps${NC}"
fi

# Check Dockerfiles
echo ""
echo "Checking Dockerfiles..."

if [ ! -f "Dockerfile.agent" ]; then
    echo -e "${RED}✗ Dockerfile.agent not found${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Dockerfile.agent exists${NC}"

if [ ! -f "Dockerfile.chatapi" ]; then
    echo -e "${RED}✗ Dockerfile.chatapi not found${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Dockerfile.chatapi exists${NC}"

if [ ! -f "Dockerfile.web" ]; then
    echo -e "${RED}✗ Dockerfile.web not found${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Dockerfile.web exists${NC}"

# Check placeholder image accessibility
echo ""
echo "Validating placeholder image accessibility..."

PLACEHOLDER_IMAGE="mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
if docker manifest inspect $PLACEHOLDER_IMAGE &> /dev/null; then
    echo -e "${GREEN}✓ Placeholder image is accessible: $PLACEHOLDER_IMAGE${NC}"
else
    echo -e "${YELLOW}! Could not verify placeholder image (docker may not be running)${NC}"
    echo "  This is OK if you're just validating configuration"
fi

# Summary
echo ""
echo "==================================="
echo "Validation Summary"
echo "==================================="
echo ""
echo -e "${GREEN}✓ All critical checks passed!${NC}"
echo ""
echo "You can now deploy with:"
echo "  azd up         # Full deployment"
echo "  azd provision  # Infrastructure only"
echo "  azd deploy     # Application only"
echo ""
echo "For more information, see:"
echo "  - docs/CONTAINER_IMAGE_DEPLOYMENT.md"
echo "  - CONTAINER_IMAGE_FIX.md"
echo ""
