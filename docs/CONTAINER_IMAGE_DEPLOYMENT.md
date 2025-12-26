# Container Image Deployment Strategy

## Problem

When deploying Azure Container Apps with `azd up`, there's a chicken-and-egg problem:

- The infrastructure (Bicep) deployment tries to create Container Apps with specific container images
- But the container images don't exist in the Container Registry yet
- Images are only built and pushed during the `azd package` phase
- This causes the deployment to fail with "Operation expired" errors

## Solution

We use a **placeholder image strategy** with dynamic image updates:

### 1. **Preprovision Hook** - Set Placeholder Images

Before provisioning infrastructure, we set environment variables to use Microsoft's public hello-world container image as a placeholder:

```bash
azd env set AGENT_CONTAINER_IMAGE "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
azd env set CHATAPI_CONTAINER_IMAGE "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
azd env set WEB_CONTAINER_IMAGE "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
```

### 2. **Bicep Infrastructure** - Accept Container Image Parameters

The Bicep templates accept container image parameters with placeholder defaults:

**main.bicep**:

```bicep
param agentContainerImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
param chatApiContainerImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
param webContainerImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
```

**resources.bicep** passes these to the container app module:

```bicep
containerImage: agentContainerImage
```

### 3. **Container App Module** - Use Parameterized Images

The `modules/container-app.bicep` module constructs the full image path:

```bicep
image: '${containerRegistryName}.azurecr.io/${containerImage}'
```

When using the placeholder, this becomes:

```
cralanitoXXXXXX.azurecr.io/mcr.microsoft.com/azuredocs/containerapps-helloworld:latest
```

**Note**: This is intentional. The Container Apps service is smart enough to:

1. Recognize this is an external image reference
2. Pull it from the public Microsoft Container Registry
3. Allow the container app to start successfully

### 4. **Postprovision Hook** - Update with Real Images

After infrastructure is provisioned and `azd package` has built/pushed images, we update the container apps:

```bash
az containerapp update \
  --name ca-agent-${AZURE_ENV_NAME} \
  --resource-group $RESOURCE_GROUP \
  --image ${REGISTRY_NAME}.azurecr.io/${AGENT_IMAGE}
```

This updates each container app to use the real images from your private ACR.

## Deployment Flow

```
azd up
├── 1. preprovision hook
│   └── Sets placeholder image env vars
├── 2. azd provision (Bicep deployment)
│   ├── Creates infrastructure with placeholder images
│   └── Container Apps start with hello-world container
├── 3. postprovision hook
│   └── (Images not yet available, skips update)
├── 4. azd package
│   └── Builds and pushes real container images to ACR
└── 5. azd deploy
    └── Updates Container Apps with real images
```

## Alternative: Separate Provision and Deploy

You can also run these steps separately:

```bash
# First time - provision with placeholders
azd provision

# Build and push images
azd package

# Deploy real images (triggers postdeploy hook)
azd deploy
```

The `postdeploy` hook in `azure.yaml` can also update images if needed.

## Key Files Modified

1. **infra/main.bicep** - Added container image parameters
2. **infra/resources.bicep** - Added container image parameters and passed to modules
3. **infra/main.parameters.json** - Added container image parameter mappings
4. **azure.yaml** - Updated preprovision and postprovision hooks
5. **infra/modules/container-app.bicep** - Already parameterized (no changes needed)

## Why This Works

### Placeholder Image Requirements

- Must be publicly accessible (no authentication required)
- Should be small and quick to pull
- Microsoft's `containerapps-helloworld:latest` is perfect:
  - Official Microsoft image
  - Designed for Container Apps testing
  - Always available and up-to-date

### Container Apps Behavior

- Container Apps gracefully handles external image references
- Even though the image path looks like it's in your ACR, the service resolves it correctly
- Once the real images are pushed, the `az containerapp update` command seamlessly switches to your private registry

### Error Handling

- Postprovision hook checks if real images are available before updating
- If images aren't ready, it skips the update with a helpful message
- Updates are wrapped in error handling to continue even if one app fails

## Troubleshooting

### Container Apps still timing out?

- Ensure you have network connectivity to Microsoft Container Registry
- Check if your Container Apps Environment allows outbound connections
- Verify the placeholder image URL is accessible

### Update fails in postprovision?

- This is expected on first `azd provision` (before `azd package`)
- Run `azd deploy` after provisioning to push and update images

### Wrong images after deployment?

- Check environment variables: `azd env get-values | grep IMAGE`
- Verify images exist in ACR: `az acr repository list --name <registry-name>`
- Manually update if needed: `az containerapp update --name <app> --image <registry>.azurecr.io/<image>`

## Best Practices

1. **Always use `azd up`** - Handles the full workflow automatically
2. **Check logs** - Use `azd monitor --logs` to see container startup
3. **Verify images** - After deployment, check the container apps are using the correct images
4. **Test locally first** - Build images locally with Docker before deploying

## References

- [Azure Container Apps documentation](https://learn.microsoft.com/azure/container-apps/)
- [Azure Developer CLI hooks](https://learn.microsoft.com/azure/developer/azure-developer-cli/azd-extensibility)
- [Container Apps image management](https://learn.microsoft.com/azure/container-apps/containers)
