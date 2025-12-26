#!/bin/bash
# ALAN Azure Deployment Helper Script
# This script simplifies the deployment process for ALAN to Azure

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default values
DEFAULT_ENV_NAME="dev"
DEFAULT_LOCATION="eastus"

# Functions
print_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_header() {
    echo ""
    echo "================================"
    echo "$1"
    echo "================================"
    echo ""
}

check_prerequisites() {
    print_header "Checking Prerequisites"
    
    # Check Azure CLI
    if ! command -v az &> /dev/null; then
        print_error "Azure CLI is not installed. Please install it from: https://learn.microsoft.com/cli/azure/install-azure-cli"
        exit 1
    fi
    print_success "Azure CLI found: $(az version --query '\"azure-cli\"' -o tsv)"
    
    # Check if logged in
    if ! az account show &> /dev/null; then
        print_error "Not logged in to Azure. Please run: az login"
        exit 1
    fi
    SUBSCRIPTION_NAME=$(az account show --query name -o tsv)
    print_success "Logged in to Azure subscription: $SUBSCRIPTION_NAME"
    
    # Check Docker (optional, for building images)
    if command -v docker &> /dev/null; then
        print_success "Docker found: $(docker --version)"
    else
        print_warning "Docker not found. You'll need it to build container images."
    fi
    
    # Check azd (optional)
    if command -v azd &> /dev/null; then
        print_success "Azure Developer CLI found: $(azd version)"
    else
        print_warning "Azure Developer CLI not found. Install it for easier deployment: https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd"
    fi
}

get_user_input() {
    print_header "Configuration"
    
    # Environment name
    read -p "Environment name (dev/staging/prod) [${DEFAULT_ENV_NAME}]: " ENV_NAME
    ENV_NAME=${ENV_NAME:-$DEFAULT_ENV_NAME}
    print_info "Environment: $ENV_NAME"
    
    # Location
    read -p "Azure location [${DEFAULT_LOCATION}]: " LOCATION
    LOCATION=${LOCATION:-$DEFAULT_LOCATION}
    print_info "Location: $LOCATION"
    
    # Get principal ID
    PRINCIPAL_ID=$(az ad signed-in-user show --query id -o tsv)
    print_info "Principal ID: $PRINCIPAL_ID"
    
    # Confirm
    echo ""
    read -p "Deploy with these settings? (y/n): " CONFIRM
    if [[ $CONFIRM != "y" && $CONFIRM != "Y" ]]; then
        print_error "Deployment cancelled."
        exit 0
    fi
}

deploy_infrastructure() {
    print_header "Deploying Infrastructure"
    
    RESOURCE_GROUP="rg-alan-${ENV_NAME}"
    
    print_info "Creating resource group: $RESOURCE_GROUP"
    az group create --name $RESOURCE_GROUP --location $LOCATION --output none
    print_success "Resource group created"
    
    print_info "Deploying Bicep templates... (this may take 10-15 minutes)"
    DEPLOYMENT_NAME="alan-${ENV_NAME}-$(date +%Y%m%d-%H%M%S)"
    
    az deployment sub create \
        --location $LOCATION \
        --name $DEPLOYMENT_NAME \
        --template-file ./infra/main.bicep \
        --parameters ./infra/main.parameters.json \
        --parameters environmentName=$ENV_NAME \
        --parameters location=$LOCATION \
        --parameters principalId=$PRINCIPAL_ID \
        --parameters principalType=User \
        --output none
    
    print_success "Infrastructure deployed successfully"
    
    # Get outputs
    print_info "Retrieving deployment outputs..."
    OUTPUTS=$(az deployment sub show --name $DEPLOYMENT_NAME --query properties.outputs -o json)
    
    export REGISTRY_NAME=$(echo $OUTPUTS | jq -r '.AZURE_CONTAINER_REGISTRY_NAME.value')
    export WEB_URL=$(echo $OUTPUTS | jq -r '.WEB_APP_URL.value')
    export OPENAI_ENDPOINT=$(echo $OUTPUTS | jq -r '.AZURE_OPENAI_ENDPOINT.value')
    export STORAGE_NAME=$(echo $OUTPUTS | jq -r '.AZURE_STORAGE_ACCOUNT_NAME.value')
    
    print_success "Deployment outputs retrieved"
}

build_and_push_images() {
    print_header "Building Container Images"
    
    if ! command -v docker &> /dev/null; then
        print_warning "Docker not found. Skipping image build."
        print_info "You can build images later using: az acr build --registry $REGISTRY_NAME --image <image-name> -f <Dockerfile> ."
        return
    fi
    
    print_info "Logging in to Azure Container Registry..."
    az acr login --name $REGISTRY_NAME
    
    print_info "Building and pushing agent image..."
    az acr build --registry $REGISTRY_NAME \
        --image alan-agent:latest \
        --file Dockerfile.agent \
        . --output none
    print_success "Agent image built and pushed"
    
    print_info "Building and pushing chatapi image..."
    az acr build --registry $REGISTRY_NAME \
        --image alan-chatapi:latest \
        --file Dockerfile.chatapi \
        . --output none
    print_success "ChatApi image built and pushed"
    
    print_info "Building and pushing web image..."
    az acr build --registry $REGISTRY_NAME \
        --image alan-web:latest \
        --file Dockerfile.web \
        . --output none
    print_success "Web image built and pushed"
}

update_container_apps() {
    print_header "Updating Container Apps"
    
    RESOURCE_GROUP="rg-alan-${ENV_NAME}"
    
    print_info "Updating agent container app..."
    az containerapp update \
        --name ca-agent-${ENV_NAME} \
        --resource-group $RESOURCE_GROUP \
        --image ${REGISTRY_NAME}.azurecr.io/alan-agent:latest \
        --output none || print_warning "Agent app update failed or app doesn't exist yet"
    
    print_info "Updating chatapi container app..."
    az containerapp update \
        --name ca-chatapi-${ENV_NAME} \
        --resource-group $RESOURCE_GROUP \
        --image ${REGISTRY_NAME}.azurecr.io/alan-chatapi:latest \
        --output none || print_warning "ChatApi app update failed or app doesn't exist yet"
    
    print_info "Updating web container app..."
    az containerapp update \
        --name ca-web-${ENV_NAME} \
        --resource-group $RESOURCE_GROUP \
        --image ${REGISTRY_NAME}.azurecr.io/alan-web:latest \
        --output none || print_warning "Web app update failed or app doesn't exist yet"
    
    print_success "Container apps updated"
}

save_outputs() {
    print_header "Saving Deployment Outputs"
    
    OUTPUT_FILE=".env.${ENV_NAME}.azure"
    
    cat > $OUTPUT_FILE << EOF
# ALAN Azure Deployment Outputs
# Environment: ${ENV_NAME}
# Generated: $(date)

# Azure OpenAI Configuration
AZURE_OPENAI_ENDPOINT="${OPENAI_ENDPOINT}"
AZURE_OPENAI_DEPLOYMENT="gpt-4o-mini"

# Azure Storage Configuration
AZURE_STORAGE_ACCOUNT_NAME="${STORAGE_NAME}"

# Container Registry
AZURE_CONTAINER_REGISTRY_NAME="${REGISTRY_NAME}"

# Application URLs
WEB_APP_URL="${WEB_URL}"

# For local development, also set:
# AZURE_TENANT_ID="$(az account show --query tenantId -o tsv)"
# AZURE_SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
EOF

    print_success "Outputs saved to: $OUTPUT_FILE"
}

print_summary() {
    print_header "Deployment Complete! 🎉"
    
    echo "Your ALAN application has been deployed to Azure."
    echo ""
    echo "Key Information:"
    echo "  Environment:        $ENV_NAME"
    echo "  Resource Group:     rg-alan-${ENV_NAME}"
    echo "  Location:           $LOCATION"
    echo ""
    echo "Application URLs:"
    echo "  Web Application:    ${WEB_URL}"
    echo ""
    echo "Next Steps:"
    echo "  1. Visit your web application: ${WEB_URL}"
    echo "  2. Check Container Apps status in Azure Portal"
    echo "  3. View logs: az containerapp logs show --name ca-web-${ENV_NAME} --resource-group rg-alan-${ENV_NAME} --follow"
    echo "  4. Update your local .env file with values from: .env.${ENV_NAME}.azure"
    echo ""
    echo "Documentation:"
    echo "  - Infrastructure details: infra/README.md"
    echo "  - Deployment guide: docs/AZURE_DEPLOYMENT.md"
    echo ""
}

# Main execution
main() {
    echo ""
    echo "╔═══════════════════════════════════════╗"
    echo "║   ALAN Azure Deployment Helper        ║"
    echo "║   Autonomous Learning Agent Network   ║"
    echo "╚═══════════════════════════════════════╝"
    
    check_prerequisites
    get_user_input
    deploy_infrastructure
    build_and_push_images
    update_container_apps
    save_outputs
    print_summary
}

# Run main function
main "$@"
