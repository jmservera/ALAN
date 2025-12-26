# Network Architecture & Connectivity

## Overview

ALAN uses a hub-and-spoke network architecture with private endpoints for all Azure services to ensure secure communication.

## Network Topology

```
VNet: 10.0.0.0/16
├── Infrastructure Subnet: 10.0.0.0/23
│   └── Service Endpoints: Storage, Cognitive Services
├── Private Endpoint Subnet: 10.0.2.0/24
│   ├── PE: Storage (Blob) → privatelink.blob.core.windows.net
│   ├── PE: Storage (Queue) → privatelink.queue.core.windows.net
│   ├── PE: Azure OpenAI → privatelink.openai.azure.com
│   └── PE: Container Registry → privatelink.azurecr.io
└── Container Apps Subnet: 10.0.4.0/23
    ├── Delegation: Microsoft.App/environments
    └── Container Apps: agent, chatapi, web
```

## Private Endpoints Configuration

### 1. Storage Account

- **Blob Private Endpoint**: Connected to `private-endpoint-subnet`
- **Queue Private Endpoint**: Connected to `private-endpoint-subnet`
- **DNS Resolution**: `privatelink.blob.core.windows.net` and `privatelink.queue.core.windows.net`
- **Access**: Container Apps → Private Endpoint → Storage

### 2. Azure OpenAI

- **Private Endpoint**: Connected to `private-endpoint-subnet`
- **DNS Resolution**: `privatelink.openai.azure.com`
- **Access**: Container Apps → Private Endpoint → Azure OpenAI

### 3. Container Registry (ACR)

- **Private Endpoint**: Connected to `private-endpoint-subnet`
- **DNS Resolution**: `privatelink.azurecr.io`
- **Access**: Container Apps → Private Endpoint → ACR
- **Public Access**: Temporarily enabled during initial provisioning to allow placeholder image pull from mcr.microsoft.com

## Container Apps Connectivity

### Subnet Configuration

- **Delegation**: `Microsoft.App/environments` - Required for Container Apps
- **Address Space**: 10.0.4.0/23 (512 IPs)
- **Connectivity**:
  - ✅ Private Endpoint Subnet (via VNet routing)
  - ✅ Internet (for public ingress to web app)
  - ✅ DNS Resolution via Private DNS Zones

### Container Apps Access Pattern

```
Container App (in container-apps-subnet)
    │
    ├─→ Storage Account
    │   └─→ Route: VNet → Private Endpoint → Storage
    │   └─→ DNS: privatelink.blob.core.windows.net → 10.0.2.x
    │
    ├─→ Azure OpenAI
    │   └─→ Route: VNet → Private Endpoint → OpenAI
    │   └─→ DNS: privatelink.openai.azure.com → 10.0.2.x
    │
    ├─→ Container Registry
    │   └─→ Route: VNet → Private Endpoint → ACR
    │   └─→ DNS: privatelink.azurecr.io → 10.0.2.x
    │
    └─→ Internet (for initial placeholder image pull)
        └─→ Route: Direct (Container Apps Environment not internal)
        └─→ DNS: Public DNS → mcr.microsoft.com
```

## DNS Resolution

### Private DNS Zones

| Zone                                 | Purpose            | Linked to VNet |
| ------------------------------------ | ------------------ | -------------- |
| `privatelink.blob.core.windows.net`  | Storage Blob       | ✅             |
| `privatelink.queue.core.windows.net` | Storage Queue      | ✅             |
| `privatelink.openai.azure.com`       | Azure OpenAI       | ✅             |
| `privatelink.azurecr.io`             | Container Registry | ✅             |

### DNS Flow

1. Container App queries `stb3u2tz6xkbguc.blob.core.windows.net`
2. CNAME points to `stb3u2tz6xkbguc.privatelink.blob.core.windows.net`
3. Private DNS Zone resolves to private endpoint IP (10.0.2.x)
4. Traffic flows through VNet to private endpoint

## Security Boundaries

### Inbound Traffic

- **Web App**: Public ingress enabled (`ingressExternal: true`)
- **ChatApi**: Internal only (`ingressExternal: false`)
- **Agent**: No ingress (`enableIngress: false`)

### Outbound Traffic

- **Storage**: Via private endpoint only
- **Azure OpenAI**: Via private endpoint only
- **Container Registry**: Via private endpoint (after initial setup)
- **Internet**: Allowed for initial provisioning (mcr.microsoft.com)

### Network Security

- ✅ All data services behind private endpoints
- ✅ No public access to Storage or OpenAI
- ✅ Container Registry accessible via private endpoint
- ⚠️ ACR temporarily has public access for initial image pull
- ✅ VNet isolation for all resources
- ✅ Managed Identity authentication (no keys/secrets)

## Initial Provisioning Flow

During `azd up`, the following connectivity is required:

1. **Container Apps Environment Creation**
   - Must create in VNet (container-apps-subnet)
   - Must have access to Log Analytics (via private endpoint or public)

2. **Image Pull for Container Apps**

   ```
   Container App needs:
   ├─→ mcr.microsoft.com (placeholder image)
   │   └─→ Requires: Internet access from Container Apps Environment
   │   └─→ Current: Allowed (internal: false)
   │
   └─→ <your-acr>.azurecr.io (real images, post-deployment)
       └─→ Requires: Private endpoint + DNS resolution
       └─→ Current: Configured with private endpoint
   ```

3. **DNS Resolution**
   - Private DNS zones must be created before private endpoints
   - Private DNS zones must be linked to VNet
   - Container Apps automatically use VNet DNS

## Post-Deployment Lockdown (Optional)

To fully lock down Container Registry after initial deployment:

### Step 1: Verify Private Endpoint is Working

```bash
# Get ACR name
ACR_NAME=$(azd env get-values | grep AZURE_CONTAINER_REGISTRY_NAME | cut -d= -f2 | tr -d '"')

# Check private endpoint status
az network private-endpoint show \
  --name pe-${ACR_NAME} \
  --resource-group rg-<env-name> \
  --query "provisioningState"

# Should return "Succeeded"
```

### Step 2: Disable Public Access

```bash
# Disable public network access
az acr update \
  --name $ACR_NAME \
  --public-network-enabled false \
  --default-action Deny
```

### Step 3: Verify Container Apps Can Still Pull

```bash
# Update a container app to verify it can pull through private endpoint
az containerapp update \
  --name ca-agent-<env-name> \
  --resource-group rg-<env-name> \
  --image ${ACR_NAME}.azurecr.io/alan-agent:latest
```

## Troubleshooting

### Container Apps Can't Pull Images

**Symptom**: "Failed to provision revision... Operation expired"

**Check List**:

1. ✅ Private endpoint exists for ACR
2. ✅ Private DNS Zone exists: `privatelink.azurecr.io`
3. ✅ DNS Zone linked to VNet
4. ✅ Managed identity has `AcrPull` role
5. ✅ ACR has public access enabled (initial deployment)

**Test DNS Resolution**:

```bash
# From a VM in the VNet or using Azure Bastion
nslookup ${ACR_NAME}.azurecr.io
# Should resolve to 10.0.2.x (private IP)
```

### Storage Connection Failures

**Symptom**: Container Apps can't access storage blobs/queues

**Check List**:

1. ✅ Private endpoints exist for blob and queue
2. ✅ Private DNS Zones exist and linked
3. ✅ Managed identity has Storage roles
4. ✅ Storage networkAcls allows AzureServices bypass

**Test Connection**:

```bash
# Check from Container App
az containerapp exec \
  --name ca-agent-<env-name> \
  --resource-group rg-<env-name> \
  --command "curl -I https://${STORAGE_NAME}.blob.core.windows.net"
```

### OpenAI Connection Failures

**Symptom**: Container Apps can't call Azure OpenAI

**Check List**:

1. ✅ Private endpoint exists for OpenAI
2. ✅ Private DNS Zone exists: `privatelink.openai.azure.com`
3. ✅ Managed identity has `Cognitive Services OpenAI User` role
4. ✅ OpenAI networkAcls allows AzureServices bypass

### DNS Not Resolving to Private IPs

**Symptom**: Services resolve to public IPs instead of private

**Solution**:

```bash
# Verify Private DNS Zone links
az network private-dns link vnet list \
  --resource-group rg-<env-name> \
  --zone-name privatelink.azurecr.io

# Should show link to your VNet
```

## Network Flow Diagrams

### Data Access Flow

```
┌─────────────────────────────────────────────────┐
│          Container Apps Subnet (10.0.4.0/23)    │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐     │
│  │  Agent   │  │ ChatApi  │  │   Web    │     │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘     │
└───────┼─────────────┼─────────────┼────────────┘
        │             │             │
        │    VNet Internal Routing  │
        │             │             │
        ▼             ▼             ▼
┌─────────────────────────────────────────────────┐
│   Private Endpoint Subnet (10.0.2.0/24)         │
│  ┌────────┐  ┌────────┐  ┌────────┐  ┌────────┐│
│  │Storage │  │Storage │  │OpenAI  │  │  ACR   ││
│  │ Blob   │  │ Queue  │  │        │  │        ││
│  └───┬────┘  └───┬────┘  └───┬────┘  └───┬────┘│
└──────┼───────────┼───────────┼───────────┼─────┘
       │           │           │           │
   PrivateLink PrivateLink PrivateLink PrivateLink
       │           │           │           │
       ▼           ▼           ▼           ▼
   [Storage]   [Storage]   [OpenAI]     [ACR]
   (Azure)     (Azure)     (Azure)    (Azure)
```

### Internet Access Flow (Initial Provisioning)

```
┌─────────────────────────────────────────────────┐
│          Container Apps Subnet (10.0.4.0/23)    │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐     │
│  │  Agent   │  │ ChatApi  │  │   Web    │     │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘     │
└───────┼─────────────┼─────────────┼────────────┘
        │             │             │
        │  Pull from mcr.microsoft.com
        │             │             │
        └─────────────┴─────────────┘
                      │
              (internal: false)
                      │
                      ▼
            ┌──────────────────┐
            │  Internet        │
            │  (mcr.microsoft) │
            └──────────────────┘
```

## Best Practices

1. **Use Private Endpoints**: All Azure PaaS services should use private endpoints
2. **Lock Down After Deployment**: Disable ACR public access after initial setup
3. **Monitor DNS Resolution**: Ensure private DNS zones are working correctly
4. **Use Managed Identity**: No connection strings or keys in configuration
5. **Test Connectivity**: Verify each service connection after deployment
6. **Document Changes**: Keep network architecture documentation up to date

## References

- [Azure Container Apps Networking](https://learn.microsoft.com/azure/container-apps/networking)
- [Azure Private Link](https://learn.microsoft.com/azure/private-link/private-link-overview)
- [Azure Private Endpoint DNS](https://learn.microsoft.com/azure/private-link/private-endpoint-dns)
- [Container Registry Private Link](https://learn.microsoft.com/azure/container-registry/container-registry-private-link)
