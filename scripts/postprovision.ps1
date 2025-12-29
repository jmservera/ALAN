# Post-provision script for ALAN (PowerShell)
# This script enables local development access to Azure resources by:
# 1. Adding current user's IP to OpenAI network rules
# 2. Adding current user's IP to Container Registry network rules
# 3. Assigning Cognitive Services OpenAI User role to current user
# 4. Assigning AcrPush role to current user for Container Registry
#
# This script only runs locally (when CI environment variable is not set)
# to ensure production deployments remain fully secured via private endpoints.

$ErrorActionPreference = "Stop"

Write-Host "=== ALAN Post-Provision Script ===" -ForegroundColor Cyan

# Check if running in CI environment
if ($env:CI) {
    Write-Host "CI environment detected - skipping development access configuration" -ForegroundColor Yellow
    Write-Host "Production deployment will use private endpoints only" -ForegroundColor Yellow
    exit 0
}

Write-Host "Local development environment detected - configuring OpenAI access" -ForegroundColor Green

# Get required values from azd environment
Write-Host ""
Write-Host "Retrieving deployment information..." -ForegroundColor Cyan

try {
    $RESOURCE_GROUP = azd env get-value AZURE_RESOURCE_GROUP 2>$null
    if ([string]::IsNullOrWhiteSpace($RESOURCE_GROUP)) { throw "AZURE_RESOURCE_GROUP not found" }
} catch {
    Write-Host "Error: Could not find AZURE_RESOURCE_GROUP in environment" -ForegroundColor Red
    exit 1
}

try {
    $OPENAI_NAME = azd env get-value AZURE_OPENAI_NAME 2>$null
    if ([string]::IsNullOrWhiteSpace($OPENAI_NAME)) { throw "AZURE_OPENAI_NAME not found" }
} catch {
    Write-Host "Error: Could not find AZURE_OPENAI_NAME in environment" -ForegroundColor Red
    exit 1
}

try {
    $ACR_NAME = azd env get-value AZURE_CONTAINER_REGISTRY_NAME 2>$null
    if ([string]::IsNullOrWhiteSpace($ACR_NAME)) { throw "AZURE_CONTAINER_REGISTRY_NAME not found" }
} catch {
    Write-Host "Error: Could not find AZURE_CONTAINER_REGISTRY_NAME in environment" -ForegroundColor Red
    exit 1
}

try {
    $PRINCIPAL_ID = azd env get-value AZURE_PRINCIPAL_ID 2>$null
} catch {
    $PRINCIPAL_ID = $null
}

Write-Host "Resource Group: $RESOURCE_GROUP" -ForegroundColor White
Write-Host "OpenAI Account: $OPENAI_NAME" -ForegroundColor White
Write-Host "Container Registry: $ACR_NAME" -ForegroundColor White

# Get current user's public IP address
Write-Host ""
Write-Host "Retrieving current user's public IP address..." -ForegroundColor Cyan

try {
    $USER_IP = (Invoke-RestMethod -Uri "https://api.ipify.org" -ErrorAction Stop).Trim()
} catch {
    Write-Host "Warning: Could not retrieve user IP address - skipping network rule configuration" -ForegroundColor Yellow
    $USER_IP = $null
}

if ($USER_IP) {
    Write-Host "Current IP: $USER_IP" -ForegroundColor White
    
    # Get OpenAI resource ID for REST API calls
    Write-Host ""
    Write-Host "Retrieving OpenAI resource information..." -ForegroundColor Cyan
    $openAiResourceId = az cognitiveservices account show `
        --name $OPENAI_NAME `
        --resource-group $RESOURCE_GROUP `
        --query id -o tsv
    
    # Enable public network access for development using REST API
    Write-Host "Enabling public network access for development..." -ForegroundColor Cyan
    az rest `
        --uri "${openAiResourceId}?api-version=2023-05-01" `
        --method PATCH `
        --body '{"properties":{"publicNetworkAccess":"Enabled"}}' `
        --headers 'Content-Type=application/json' `
        --only-show-errors 2>$null | Out-Null
    
    # Wait for operation to complete
    Write-Host "Waiting for public network access to be enabled..." -ForegroundColor Cyan
    Start-Sleep -Seconds 5
    
    # Check if IP is already in network rules
    Write-Host "Checking OpenAI network rules..." -ForegroundColor Cyan
    $existingIp = az cognitiveservices account show `
        --name $OPENAI_NAME `
        --resource-group $RESOURCE_GROUP `
        --query "properties.networkAcls.ipRules[?value=='$USER_IP'].value" -o tsv 2>$null
    
    if ($existingIp -eq $USER_IP) {
        Write-Host "✓ IP $USER_IP already configured in network rules" -ForegroundColor Green
    } else {
        # Add current IP to network rules
        Write-Host "Adding current IP to OpenAI network rules..." -ForegroundColor Cyan
        $addResult = az cognitiveservices account network-rule add `
            --name $OPENAI_NAME `
            --resource-group $RESOURCE_GROUP `
            --ip-address $USER_IP `
            --only-show-errors 2>$null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Network access configured for IP: $USER_IP" -ForegroundColor Green
        } else {
            Write-Host "Warning: Could not add IP to network rules (may already exist or insufficient permissions)" -ForegroundColor Yellow
        }
    }
}

# Configure Container Registry access
if (-not [string]::IsNullOrWhiteSpace($USER_IP)) {
    Write-Host "" 
    Write-Host "=== Configuring Container Registry Access ===" -ForegroundColor Cyan
    
    # Get ACR resource ID
    Write-Host "Retrieving Container Registry information..." -ForegroundColor Cyan
    $ACR_RESOURCE_ID = az acr show `
        --name $ACR_NAME `
        --resource-group $RESOURCE_GROUP `
        --query id -o tsv
    
    # Check if IP is already in network rules
    Write-Host "Checking Container Registry network rules..." -ForegroundColor Cyan
    $EXISTING_ACR_IP = az acr network-rule list `
        --name $ACR_NAME `
        --resource-group $RESOURCE_GROUP `
        --query "ipRules[?ipAddressOrRange=='$USER_IP/32'].ipAddressOrRange" -o tsv 2>$null
    
    if ($EXISTING_ACR_IP) {
        Write-Host "✓ IP $USER_IP already configured in ACR network rules" -ForegroundColor Green
    } else {
        # Add current IP to ACR network rules
        Write-Host "Adding current IP to Container Registry network rules..." -ForegroundColor Cyan
        try {
            $addAcrResult = az acr network-rule add `
                --name $ACR_NAME `
                --ip-address $USER_IP 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✓ Container Registry network access configured for IP: $USER_IP" -ForegroundColor Green
            } else {
                Write-Host "Warning: Could not add IP to ACR network rules (may already exist or insufficient permissions)" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "Warning: Could not add IP to ACR network rules (may already exist or insufficient permissions)" -ForegroundColor Yellow
        }
    }
}

# Assign Cognitive Services OpenAI User role to current user (if not already assigned)
if (-not [string]::IsNullOrWhiteSpace($PRINCIPAL_ID)) {
    Write-Host ""
    Write-Host "Assigning Cognitive Services OpenAI User role to current user..." -ForegroundColor Cyan
    Write-Host "Principal ID: $PRINCIPAL_ID" -ForegroundColor White
    
    # Get the OpenAI resource ID
    $OPENAI_RESOURCE_ID = az cognitiveservices account show `
        --name $OPENAI_NAME `
        --resource-group $RESOURCE_GROUP `
        --query id -o tsv
    
    # Check if role assignment already exists
    $EXISTING_ASSIGNMENT = az role assignment list `
        --assignee $PRINCIPAL_ID `
        --scope $OPENAI_RESOURCE_ID `
        --role "Cognitive Services OpenAI User" `
        --query "[0].id" -o tsv 2>$null
    
    if ($EXISTING_ASSIGNMENT) {
        Write-Host "✓ Role already assigned to user" -ForegroundColor Green
    } else {
        # Assign the role
        az role assignment create `
            --assignee $PRINCIPAL_ID `
            --role "Cognitive Services OpenAI User" `
            --scope $OPENAI_RESOURCE_ID `
            --only-show-errors 2>$null | Out-Null
        
        Write-Host "✓ Role assigned successfully" -ForegroundColor Green
    }
    
    # Assign AcrPush role to current user for Container Registry
    Write-Host "" 
    Write-Host "Assigning AcrPush role to current user for Container Registry..." -ForegroundColor Cyan
    
    # Get the ACR resource ID
    $ACR_RESOURCE_ID = az acr show `
        --name $ACR_NAME `
        --resource-group $RESOURCE_GROUP `
        --query id -o tsv
    
    # Check if role assignment already exists
    $EXISTING_ACR_ASSIGNMENT = az role assignment list `
        --assignee $PRINCIPAL_ID `
        --scope $ACR_RESOURCE_ID `
        --role "AcrPush" `
        --query "[0].id" -o tsv 2>$null
    
    if ($EXISTING_ACR_ASSIGNMENT) {
        Write-Host "✓ AcrPush role already assigned to user" -ForegroundColor Green
    } else {
        # Assign the role
        az role assignment create `
            --assignee $PRINCIPAL_ID `
            --role "AcrPush" `
            --scope $ACR_RESOURCE_ID `
            --only-show-errors 2>$null | Out-Null
        
        Write-Host "✓ AcrPush role assigned successfully" -ForegroundColor Green
    }
} else {
    Write-Host ""
    Write-Host "Warning: AZURE_PRINCIPAL_ID not found - skipping role assignment" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Post-provision configuration complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Development access enabled for:" -ForegroundColor Green
if ($USER_IP) {
    Write-Host "  - IP Address: $USER_IP (OpenAI + Container Registry)" -ForegroundColor White
}
if ($PRINCIPAL_ID) {
    Write-Host "  - User Principal: $PRINCIPAL_ID" -ForegroundColor White
    Write-Host "  - Roles: Cognitive Services OpenAI User, AcrPush" -ForegroundColor White
}
Write-Host ""
Write-Host "You can now push images to ACR using:" -ForegroundColor Cyan
Write-Host "  az acr login --name $ACR_NAME" -ForegroundColor White
Write-Host "  docker tag <image> ${ACR_NAME}.azurecr.io/<image>" -ForegroundColor White
Write-Host "  docker push ${ACR_NAME}.azurecr.io/<image>" -ForegroundColor White
Write-Host ""
Write-Host "Note: These settings are for local development only." -ForegroundColor Yellow
Write-Host "Production deployments will use private endpoints and managed identity." -ForegroundColor Yellow
Write-Host ""
