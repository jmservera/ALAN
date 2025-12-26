# Azure Verified Modules (AVM) Used in ALAN Infrastructure

This document lists all Azure Verified Modules (AVM) used in the ALAN infrastructure templates and their versions.

## Module References

All modules are referenced from the public Bicep registry: `br/public:avm/res/`

### Current Modules

| Module | Version | Purpose | Location in Code |
|--------|---------|---------|------------------|
| `managed-identity/user-assigned-identity` | 0.4.0 | User-assigned managed identity for Container Apps | `infra/resources.bicep:69` |
| `operational-insights/workspace` | 0.9.1 | Log Analytics workspace for monitoring | `infra/resources.bicep:79` |
| `network/virtual-network` | 0.5.2 | Virtual Network with subnets | `infra/resources.bicep:91` |
| `storage/storage-account` | 0.14.3 | Storage Account with private endpoints | `infra/resources.bicep:135` |
| `network/private-dns-zone` | 0.6.0 | Private DNS zones for internal resolution | `infra/resources.bicep:235,250,265` |
| `cognitive-services/account` | 0.9.1 | Azure OpenAI service | `infra/resources.bicep:280` |
| `container-registry/registry` | 0.7.1 | Azure Container Registry | `infra/resources.bicep:344` |
| `app/managed-environment` | 0.8.2 | Container Apps Environment | `infra/resources.bicep:364` |

## Version Selection Criteria

Versions are selected based on:
1. **Stability**: Using stable, production-ready versions
2. **Features**: Ensuring all required features are available
3. **Security**: Latest versions with security updates
4. **Compatibility**: Ensuring modules work together

## Updating Modules

To update module versions:

1. Check the [Azure Verified Modules registry](https://azure.github.io/Azure-Verified-Modules/indexes/bicep/) for latest versions
2. Review release notes and breaking changes
3. Update version in `infra/resources.bicep`
4. Test deployment in development environment
5. Run security scan: `./scripts/security-check.sh`
6. Validate with `az bicep build --file infra/main.bicep`

## Module Documentation

Each module has comprehensive documentation available:

- **Managed Identity**: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/managed-identity/user-assigned-identity
- **Log Analytics**: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/operational-insights/workspace
- **Virtual Network**: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/network/virtual-network
- **Storage Account**: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/storage/storage-account
- **Private DNS Zone**: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/network/private-dns-zone
- **Cognitive Services**: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/cognitive-services/account
- **Container Registry**: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/container-registry/registry
- **Container Apps Environment**: https://github.com/Azure/bicep-registry-modules/tree/main/avm/res/app/managed-environment

## Benefits of Using AVM

1. **Microsoft-maintained**: Official modules maintained by Azure team
2. **Best practices**: Follow Azure Well-Architected Framework principles
3. **Security**: Built-in security features and configurations
4. **Consistency**: Standardized parameters and outputs
5. **Testing**: Thoroughly tested across Azure regions
6. **Documentation**: Comprehensive documentation and examples

## Version Update History

| Date | Module | Old Version | New Version | Reason |
|------|--------|-------------|-------------|--------|
| 2024-12-16 | (initial) | - | Various | Initial implementation |

## Security Scanning

All infrastructure templates using AVM modules are scanned with Checkov to ensure:
- Proper security configurations
- Compliance with Azure best practices
- Network isolation and encryption
- Access control and identity management

Run security scan:
```bash
./scripts/security-check.sh
```

## References

- [Azure Verified Modules Initiative](https://aka.ms/avm)
- [Bicep Registry](https://github.com/Azure/bicep-registry-modules)
- [AVM Specifications](https://azure.github.io/Azure-Verified-Modules/)
