# Example Configuration

This file shows example configurations for different scenarios.

## Development Configuration (Local Testing)

Use in-memory storage and no Azure services:

```json
{
  "Agent": {
    "Azure": {
      "OpenAIEndpoint": "",
      "OpenAIKey": "",
      "OpenAIDeploymentName": "gpt-4"
    },
    "GitHub": {
      "EnableSelfImprovement": false
    },
    "Loop": {
      "IterationDelaySeconds": 10,
      "BatchProcessingIntervalIterations": 50,
      "EnableBatchProcessing": false
    },
    "Memory": {
      "ShortTermMaxEntries": 100,
      "UseLongTermMemory": false
    }
  }
}
```

## Production Configuration (Full Features)

```json
{
  "Agent": {
    "Azure": {
      "OpenAIEndpoint": "https://your-resource.openai.azure.com/",
      "OpenAIKey": "your-api-key",
      "OpenAIDeploymentName": "gpt-4",
      "StorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=youraccount;AccountKey=yourkey;EndpointSuffix=core.windows.net",
      "SearchEndpoint": "https://your-search.search.windows.net",
      "SearchKey": "your-search-key"
    },
    "GitHub": {
      "Token": "github_pat_xxxxxxxxxxxxx",
      "RepositoryOwner": "yourusername",
      "RepositoryName": "ALAN",
      "BranchName": "main",
      "EnableSelfImprovement": true
    },
    "Loop": {
      "IterationDelaySeconds": 30,
      "BatchProcessingIntervalIterations": 100,
      "EnableBatchProcessing": true
    },
    "Memory": {
      "ShortTermMaxEntries": 1000,
      "UseLongTermMemory": true
    }
  }
}
```

## Environment Variables

Instead of appsettings.json, you can use environment variables:

```bash
export Agent__Azure__OpenAIEndpoint="https://your-resource.openai.azure.com/"
export Agent__Azure__OpenAIKey="your-api-key"
export Agent__Azure__OpenAIDeploymentName="gpt-4"
export Agent__Azure__StorageConnectionString="DefaultEndpointsProtocol=https;..."
export Agent__GitHub__Token="github_pat_xxxxxxxxxxxxx"
export Agent__GitHub__RepositoryOwner="yourusername"
export Agent__GitHub__RepositoryName="ALAN"
export Agent__GitHub__EnableSelfImprovement="true"
```

## Security Best Practices

1. **Never commit secrets to Git**
   - Use `appsettings.local.json` (gitignored)
   - Use environment variables
   - Use Azure Key Vault for production

2. **GitHub Token Scopes**
   - Required: `repo` (for repository access)
   - Required: `workflow` (for PR creation)
   - Optional: `read:org` (for organization repos)

3. **Azure Storage**
   - Use Azure Managed Identity when possible
   - Rotate keys regularly
   - Enable Azure Storage firewall rules

4. **API Security**
   - Add authentication middleware in production
   - Use HTTPS only
   - Implement rate limiting
   - Enable CORS carefully
