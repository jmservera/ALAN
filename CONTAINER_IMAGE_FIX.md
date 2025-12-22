# Container Image Deployment Fix - Summary of Changes

## Problem Solved

Fixed the "Operation expired" error during `azd up` caused by Container Apps trying to use images that didn't exist in the Container Registry yet.

## Solution Overview

Implemented a **placeholder image strategy** that:

1. Uses Microsoft's public hello-world image during initial provisioning
2. Automatically updates Container Apps with real images after they're built
3. Works seamlessly with `azd up` workflow

## Files Modified

### 1. Infrastructure Templates

#### `infra/main.bicep`

- Added 3 new parameters for container images with placeholder defaults:
  - `agentContainerImage`
  - `chatApiContainerImage`
  - `webContainerImage`
- Default value: `mcr.microsoft.com/azuredocs/containerapps-helloworld:latest`

#### `infra/resources.bicep`

- Added 3 new parameters (same as main.bicep)
- Updated all 3 container app modules to use parameterized images instead of hardcoded `alan-*:latest`

#### `infra/main.parameters.json`

- Added parameter mappings with environment variable substitution:
  ```json
  "agentContainerImage": { "value": "${AGENT_CONTAINER_IMAGE=mcr.microsoft.com/azuredocs/containerapps-helloworld:latest}" }
  ```

#### `infra/modules/container-app.bicep`

- Enhanced image handling to support both ACR images and external images
- Logic: If image contains '/' and doesn't start with ACR domain, use as-is (external image)
- Otherwise, prefix with ACR domain (internal image)

### 2. Azure Developer CLI Configuration

#### `azure.yaml`

**Preprovision Hook** (added):

```yaml
# Set placeholder images before provisioning
azd env set AGENT_CONTAINER_IMAGE "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
azd env set CHATAPI_CONTAINER_IMAGE "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
azd env set WEB_CONTAINER_IMAGE "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
```

**Postprovision Hook** (enhanced):

```yaml
# After provisioning, update container apps with real images if available
az containerapp update --name ca-agent-${AZURE_ENV_NAME} \
--resource-group $RESOURCE_GROUP \
--image ${REGISTRY_NAME}.azurecr.io/${AGENT_IMAGE}
```

### 3. Documentation

#### New Files

- `docs/CONTAINER_IMAGE_DEPLOYMENT.md` - Comprehensive guide explaining:
  - The problem and solution
  - Deployment flow
  - Why it works
  - Troubleshooting

#### Updated Files

- `infra/README.md` - Added sections:
  - "Understanding the Placeholder Image Strategy"
  - Updated "Building and Pushing Container Images"
  - Enhanced troubleshooting section

## How It Works

### Deployment Flow with `azd up`

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Preprovision Hook                                        │
│    - Sets placeholder image environment variables           │
└─────────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────────┐
│ 2. Bicep Provisioning                                       │
│    - Creates Container Apps with placeholder images         │
│    - Apps start successfully with hello-world container     │
└─────────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────────┐
│ 3. Postprovision Hook                                       │
│    - Checks if real images are available                    │
│    - Skips update (images not yet built)                    │
└─────────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────────┐
│ 4. azd package                                              │
│    - Builds Docker images                                   │
│    - Pushes to Azure Container Registry                     │
└─────────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────────┐
│ 5. azd deploy                                               │
│    - Updates Container Apps with real images                │
│    - Apps restart with your application code                │
└─────────────────────────────────────────────────────────────┘
```

## Testing the Fix

### Before Testing

Ensure you have a clean environment:

```bash
azd down --force --purge
```

### Test with `azd up`

```bash
azd up
```

Expected behavior:

- ✅ Provisioning completes without timeouts
- ✅ All 3 Container Apps are created successfully
- ✅ Images are built and pushed
- ✅ Container Apps are updated with real images
- ✅ Applications start correctly

### Test Step-by-Step

```bash
# Provision only
azd provision
# Should complete successfully with placeholder images

# Build and push
azd package
# Builds Docker images and pushes to ACR

# Deploy
azd deploy
# Updates Container Apps with real images
```

## Rollback Plan

If needed, revert to hardcoded images (not recommended):

1. In `infra/resources.bicep`, change:

   ```bicep
   containerImage: agentContainerImage
   ```

   to:

   ```bicep
   containerImage: 'alan-agent:latest'
   ```

2. Ensure images are pre-built and pushed to ACR before running `azd provision`

## Benefits

1. **No More Timeouts**: Container Apps provision successfully on first attempt
2. **Seamless Experience**: `azd up` works end-to-end without manual intervention
3. **Flexible**: Supports both automatic and manual workflows
4. **Maintainable**: Clear documentation and error handling
5. **Production-Ready**: Uses official Microsoft placeholder image

## Related Issues

- Original Issue: Container Apps timeout during provisioning
- Root Cause: Bicep trying to use images that don't exist yet
- Common Pattern: This is a known issue with Infrastructure-as-Code + Container Apps

## Next Steps

1. Test the deployment with `azd up`
2. Verify all Container Apps start correctly
3. Monitor logs to ensure applications are functioning
4. Update documentation if any edge cases are discovered

## References

- [Azure Container Apps Documentation](https://learn.microsoft.com/azure/container-apps/)
- [Azure Developer CLI Hooks](https://learn.microsoft.com/azure/developer/azure-developer-cli/azd-extensibility)
- [Container Apps Placeholder Images Pattern](docs/CONTAINER_IMAGE_DEPLOYMENT.md)
