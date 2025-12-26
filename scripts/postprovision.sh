#!/bin/bash
set -e

# Post-provision script for ALAN
# This script enables local development access to Azure OpenAI by:
# 1. Adding current user's IP to OpenAI network rules
# 2. Assigning Cognitive Services OpenAI User role to current user
#
# This script only runs locally (when CI environment variable is not set)
# to ensure production deployments remain fully secured via private endpoints.

echo "=== ALAN Post-Provision Script ==="

# Check if running in CI environment
if [ -n "$CI" ]; then
  echo "CI environment detected - skipping development access configuration"
  echo "Production deployment will use private endpoints only"
  exit 0
fi

echo "Local development environment detected - configuring OpenAI access"

# Get required values from azd environment
RESOURCE_GROUP=$(azd env get-value AZURE_RESOURCE_GROUP 2>/dev/null || echo "")
OPENAI_NAME=$(azd env get-value AZURE_OPENAI_NAME 2>/dev/null || echo "")
PRINCIPAL_ID=$(azd env get-value AZURE_PRINCIPAL_ID 2>/dev/null || echo "")

if [ -z "$RESOURCE_GROUP" ]; then
  echo "Error: Could not find AZURE_RESOURCE_GROUP in environment"
  exit 1
fi

if [ -z "$OPENAI_NAME" ]; then
  echo "Error: Could not find AZURE_OPENAI_NAME in environment"
  exit 1
fi

echo "Resource Group: $RESOURCE_GROUP"
echo "OpenAI Account: $OPENAI_NAME"

# Get current user's public IP address
echo ""
echo "Retrieving current user's public IP address..."
USER_IP=$(dig +short myip.opendns.com @resolver1.opendns.com 2>/dev/null)
if [ -z "$USER_IP" ]; then
  # Fallback to curl if dig is not available
  USER_IP=$(curl -s https://api.ipify.org 2>/dev/null)
fi

if [ -z "$USER_IP" ]; then
  echo "Warning: Could not retrieve user IP address - skipping network rule configuration"
else
  echo "Current IP: $USER_IP"
  
  # Get OpenAI resource ID for REST API calls
  echo ""
  echo "Retrieving OpenAI resource information..."
  OPENAI_RESOURCE_ID=$(az cognitiveservices account show \
    --name "$OPENAI_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query id -o tsv)
  
  # Enable public network access for development using REST API
  echo "Enabling public network access for development..."
  az rest \
    --uri "${OPENAI_RESOURCE_ID}?api-version=2023-05-01" \
    --method PATCH \
    --body '{"properties":{"publicNetworkAccess":"Enabled"}}' \
    --headers 'Content-Type=application/json' \
    > /dev/null 2>&1
  
  # Wait for operation to complete
  echo "Waiting for public network access to be enabled..."
  sleep 5
  
  # Check if IP is already in network rules
  echo "Checking OpenAI network rules..."
  EXISTING_IP=$(az cognitiveservices account show \
    --name "$OPENAI_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query "properties.networkAcls.ipRules[?value=='$USER_IP'].value" -o tsv 2>/dev/null || echo "")
  
  if [ "$EXISTING_IP" = "$USER_IP" ]; then
    echo "✓ IP $USER_IP already configured in network rules"
  else
    # Add current IP to network rules
    echo "Adding current IP to OpenAI network rules..."
    if az cognitiveservices account network-rule add \
      --name "$OPENAI_NAME" \
      --resource-group "$RESOURCE_GROUP" \
      --ip-address "$USER_IP" \
      > /dev/null 2>&1; then
      echo "✓ Network access configured for IP: $USER_IP"
    else
      echo "Warning: Could not add IP to network rules (may already exist or insufficient permissions)"
    fi
  fi
fi

# Assign Cognitive Services OpenAI User role to current user (if not already assigned)
if [ -n "$PRINCIPAL_ID" ]; then
  echo ""
  echo "Assigning Cognitive Services OpenAI User role to current user..."
  echo "Principal ID: $PRINCIPAL_ID"
  
  # Get the OpenAI resource ID
  OPENAI_RESOURCE_ID=$(az cognitiveservices account show \
    --name "$OPENAI_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query id -o tsv)
  
  # Check if role assignment already exists
  EXISTING_ASSIGNMENT=$(az role assignment list \
    --assignee "$PRINCIPAL_ID" \
    --scope "$OPENAI_RESOURCE_ID" \
    --role "Cognitive Services OpenAI User" \
    --query "[0].id" -o tsv 2>/dev/null || echo "")
  
  if [ -n "$EXISTING_ASSIGNMENT" ]; then
    echo "✓ Role already assigned to user"
  else
    # Assign the role
    az role assignment create \
      --assignee "$PRINCIPAL_ID" \
      --role "Cognitive Services OpenAI User" \
      --scope "$OPENAI_RESOURCE_ID" \
      > /dev/null 2>&1
    
    echo "✓ Role assigned successfully"
  fi
else
  echo ""
  echo "Warning: AZURE_PRINCIPAL_ID not found - skipping role assignment"
fi

echo ""
echo "=== Post-provision configuration complete ==="
echo ""
echo "Development access enabled for:"
if [ -n "$USER_IP" ]; then
  echo "  - IP Address: $USER_IP"
fi
if [ -n "$PRINCIPAL_ID" ]; then
  echo "  - User Principal: $PRINCIPAL_ID"
fi
echo ""
echo "Note: These settings are for local development only."
echo "Production deployments will use private endpoints and managed identity."
echo ""
