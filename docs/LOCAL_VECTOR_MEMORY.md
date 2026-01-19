# Local Vector Memory with Qdrant

This guide explains how to run ALAN with local vector memory using Qdrant for development.

## Overview

ALAN supports two vector memory backends:
- **Qdrant** (local development) - Self-hosted, lightweight, ideal for local testing
- **Azure AI Search** (production) - Fully managed Azure service for production workloads

## Running with Qdrant Locally

### Prerequisites

- Docker and Docker Compose installed
- Azure OpenAI endpoint for embeddings (text-embedding-ada-002)

### Configuration

1. Set environment variables in `.env`:

```bash
# Azure OpenAI for embeddings
AZURE_OPENAI_ENDPOINT="https://your-instance.openai.azure.com/"
AZURE_OPENAI_EMBEDDING_DEPLOYMENT="text-embedding-ada-002"

# Qdrant local endpoint
QDRANT_ENDPOINT="http://localhost:6333"
```

2. Start services with Docker Compose:

```bash
docker-compose up -d
```

This starts:
- **Azurite** (port 10000) - Local Azure Storage emulator
- **Qdrant** (port 6333) - Local vector database
- **ALAN Agent** - Background agent service
- **ALAN ChatApi** - API service  
- **ALAN Web** - Web UI

### Qdrant UI

Access the Qdrant web UI at http://localhost:6333/dashboard to:
- View collections and points
- Inspect vectors and payloads
- Monitor search performance

### Data Persistence

Vector data is stored in the `qdrant-data` Docker volume. To reset:

```bash
docker-compose down -v  # Remove volumes
docker-compose up -d     # Start fresh
```

## Implementation Notes

### Current Status

The infrastructure for Qdrant is configured:
- Docker Compose includes Qdrant service
- Environment variables configured for local use  
- Program.cs updated to detect and use Qdrant when available
- Falls back gracefully to traditional memory if not configured

### QdrantMemoryService Implementation

A complete `QdrantMemoryService` implementation is available implementing `IVectorMemoryService` for:
- Storing memories with vector embeddings
- Semantic search with cosine similarity
- Memory retrieval and deletion
- Access tracking

The service automatically:
1. Creates the "alan-memories" collection on first use
2. Generates embeddings using Azure OpenAI
3. Stores memory metadata and vectors
4. Provides semantic search capabilities

### Configuration Priority

ALAN checks for vector memory in this order:
1. **QDRANT_ENDPOINT** - If set, uses Qdrant (local dev)
2. **AZURE_AI_SEARCH_ENDPOINT** - If set, uses Azure AI Search (production)
3. **None** - Falls back to blob-based memory (no vector search)

## Troubleshooting

### Qdrant not starting

```bash
# Check Qdrant logs
docker-compose logs qdrant

# Verify port not in use
lsof -i :6333

# Restart service
docker-compose restart qdrant
```

### Connection errors

```bash
# Verify Qdrant is running
curl http://localhost:6333/collections

# Check agent logs
docker-compose logs alanagent
```

### Memory not persisting

Ensure the `qdrant-data` volume exists:
```bash
docker volume ls | grep qdrant-data
```

## Production Deployment

For production use Azure AI Search instead of Qdrant:

1. Deploy infrastructure:
```bash
azd provision
```

2. Azure AI Search is automatically configured with:
   - Private endpoint for security
   - Managed identity authentication
   - Vector search capabilities
   - Production-grade reliability

See `infra/README.md` for full deployment details.

## Benefits of Local Qdrant

**Development advantages:**
- Fast iteration without cloud costs
- No internet required for basic testing
- Easy to reset and experiment
- Visual UI for debugging
- Open source and well-documented

**When to use:**
- Local development and testing
- CI/CD pipelines
- Demo environments
- Learning vector search concepts

**When to use Azure AI Search:**
- Production workloads
- Multi-region deployments
- Enterprise security requirements
- Managed infrastructure preference

## Next Steps

1. Start with Qdrant locally for development
2. Test memory search and retrieval
3. Deploy to Azure AI Search for production
4. Use hybrid approach (local dev + cloud prod)

For more information:
- Qdrant documentation: https://qdrant.tech/documentation/
- Azure AI Search docs: https://learn.microsoft.com/azure/search/
- ALAN vector memory guide: `VECTOR_MEMORY_GUIDE.md`
