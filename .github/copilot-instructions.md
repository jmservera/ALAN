# Copilot Instructions for ALAN Project

## Issue Tracking

This project uses **bd (beads)** for issue tracking.
Run `bd prime` for workflow context, or install hooks (`bd hooks install`) for auto-injection.

**Quick reference:**
- `bd ready` - Find unblocked work
- `bd create "Title" --type task --priority 2` - Create issue
- `bd close <id>` - Complete work
- `bd sync` - Sync with git (run at session end)

For full workflow details: `bd prime`

## Landing the Plane (Session Completion)

**When ending a work session**, you MUST complete ALL steps below. Work is NOT complete until `git push` succeeds.

**MANDATORY WORKFLOW:**

1. **File issues for remaining work** - Create issues for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **PUSH TO REMOTE** - This is MANDATORY:
   ```bash
   git pull --rebase
   bd sync
   git push
   git status  # MUST show "up to date with origin"
   ```
5. **Clean up** - Clear stashes, prune remote branches
6. **Verify** - All changes committed AND pushed
7. **Hand off** - Provide context for next session

**CRITICAL RULES:**
- Work is NOT complete until `git push` succeeds
- NEVER stop before pushing - that leaves work stranded locally
- NEVER say "ready to push when you are" - YOU must push
- If push fails, resolve and retry until it succeeds

## Project Overview

ALAN (Autonomous Learning Agent Network) is a Semantic Kernel-based autonomous agent solution that demonstrates continuous AI agent operation with real-time observability.

### Architecture Components

1. **ALAN.Agent** (Main Process)
   - Background service running an autonomous agent loop
   - Uses Semantic Kernel with Azure OpenAI for AI capabilities
   - Persists thoughts and actions to Azure Blob Storage (via Azurite locally)
   - Implements cost control through `UsageTracker`
   - Core logic in `AutonomousAgent.cs`

2. **ALAN.ChatApi** (Backend API)
   - ASP.NET Core web API with REST endpoints
   - Provides all server-side logic for the web interface
   - Background service (`AgentStateService`) polls storage for agent state
   - Exposes CopilotKit endpoint at `/copilotkit` for AG-UI protocol
   - REST API controllers for state, human input, and code proposals
   - Entry point: `Program.cs`
   - Ports: 5041 (HTTP), 5042 (HTTPS)

3. **ALAN.Web** (Next.js Frontend)
   - Next.js 16 application with TypeScript using App Router
   - Built with Next.js for fast development and production builds
   - Real-time polling of agent state from ALAN.ChatApi
   - CopilotKit integration for AI chat assistance via AG-UI protocol
   - No C# code - pure Next.js/React TypeScript application
   - Entry point: `src/app/page.tsx`
   - Port: 5269
   - **Standalone output** enabled for Docker deployment

4. **ALAN.Shared**
   - Shared models between agent and API
   - Contains `AgentState.cs`, `AgentThought.cs`, `AgentAction.cs`
   - Shared `PromptService` for Handlebars template rendering

## Running and Debugging

### Prerequisites

- .NET 8.0 SDK
- Node.js 18+ and npm for ALAN.Web Next.js frontend
- Azure OpenAI endpoint (typically uses managed identity)
- VS Code with Azurite extension installed
- Environment variables from `.env` file

### Debug Configuration

The solution uses VS Code's multi-target debugging. Configuration files:

- `.vscode/launch.json` - Debug configurations
- `.vscode/tasks.json` - Build tasks

**To start debugging:**

1. **Ensure Azurite is running**
   - Check if port 10000 is open and active
   - If not working, ask user to start the Azurite extension (not restart VS Code)
   - DO NOT troubleshoot Azurite itself - only verify port 10000 status

2. **Install Next.js frontend dependencies (first time only)**

   ```bash
   cd src/ALAN.Web
   npm install
   ```

3. **Set environment variables**
   - Copy values from `.env` to your environment
   - Required: `AZURE_OPENAI_ENDPOINT`, managed identity credentials
   - Required: `AZURE_STORAGE_CONNECTION_STRING` for Azure Blob Storage (Azurite locally)
     - Local storage uses Azurite with the default development storage connection string

4. **Launch all services**
   - Use the "Launch Agent + ChatApi + Web" compound launch configuration
   - Or run individually:
     - "C#: Launch ALAN.Agent" - Starts `ALAN.Agent/Program.cs`
     - "C#: Launch ALAN.ChatApi" - Starts `ALAN.ChatApi/Program.cs`
     - "Launch ALAN.Web (Next.js)" - Starts Next.js dev server and Chrome debugger

### Common Debugging Scenarios

**Agent Loop Issues:**

- Check `AutonomousAgent.RunAsync()` infinite loop
- Verify `UsageTracker` isn't throttling (see `COST_CONTROL.md`)
- Look at `StateManager` for state persistence

**Storage Connection Problems:**

- Verify Azurite is running on port 10000 (DO NOT restart it)
- Check connection string in environment variables
- Review blob storage files in `__blobstorage__` directory

**Next.js UI Not Updating:**

- Check API connection in browser console (should poll `/api/state` every second)
- Verify ALAN.ChatApi is running on port 5001
- Review `AgentStateService` polling logic in ChatApi
- Check Next.js rewrites configuration in `next.config.ts`

**Azure OpenAI Connection:**

- Ensure managed identity has access to Azure OpenAI
- Verify endpoint configuration in `Program.cs`
- Check deployment name matches configuration

**CopilotKit Not Working:**

- Verify `/copilotkit` endpoint is accessible on ChatApi
- Check WebSocket connection in browser dev tools
- Ensure Azure OpenAI is properly configured

## Development Tools

### MCP Services Integration

**Playwright MCP** - For web debugging:

- Use for testing UI interactions in `Index.cshtml`
- Debug SignalR connections and real-time updates
- Test responsive behavior of thoughts/actions panels

**Context7 MCP** - For codebase understanding:

- Quick navigation of Semantic Kernel integration
- Understanding agent loop flow across files
- Tracing state management through components

**Microsoft Learn MCP Server** - For documentation:

- Semantic Kernel API references
- ASP.NET Core SignalR guidance
- Azure OpenAI integration patterns

## Key Code Paths

### Agent Execution Flow

1. `AgentHostedService.ExecuteAsync()` starts the service
2. `AutonomousAgent.RunAsync()` runs the infinite loop
3. **`LoadRecentMemoriesAsync()` loads accumulated knowledge** from long-term storage at startup
4. `UsageTracker.CanExecuteLoop()` checks daily limits
5. `ThinkAndActAsync()` generates thoughts and actions **with memory context** from previous iterations
6. `StateManager` stores thoughts/actions to **short-term memory only** (8-hour TTL)
7. **Memory refresh** occurs every 10 iterations or hourly to keep context current
8. `MemoryConsolidationService.ConsolidateShortTermMemoryAsync()` runs every 6 hours to:
   - Read thoughts and actions from short-term memory
   - Evaluate importance of each item
   - Promote important items (importance ≥ 0.5) to long-term memory with "consolidated" tag
   - Extract learnings from consolidated memories

### Memory Context in Agent Loop

The agent maintains continuity across iterations through:

- **Initial Load**: Loads top 20 memories at startup (learnings, successes, reflections, decisions)
- **Periodic Refresh**: Updates memory context every 10 iterations or hourly
- **Weighted Selection**: Combines importance (70%) and recency (30%) to prioritize relevant memories
- **Prompt Integration**: Includes formatted memory context in each `ThinkAndActAsync()` prompt
- **Additive Knowledge**: Memories are append-only, never overwritten

Configuration constants in `AutonomousAgent.cs`:

- `MAX_MEMORY_CONTEXT_SIZE = 20` - Max memories included in context
- `MEMORY_REFRESH_INTERVAL_ITERATIONS = 10` - Iterations between refreshes
- `MEMORY_REFRESH_INTERVAL_HOURS = 1` - Hours between refreshes
- `IMPORTANCE_WEIGHT = 0.7` - Weight for importance in memory scoring
- `RECENCY_WEIGHT = 0.3` - Weight for recency in memory scoring
- `HIGH_IMPORTANCE_THRESHOLD = 0.8` - Threshold for including full content

### Web Update Flow (React App)

1. `AgentStateService` (in ChatApi) polls **short-term memory** every 500ms
2. Retrieves current state from `agent:current-state` key
3. Retrieves thoughts from `thought:*` keys
4. Retrieves actions from `action:*` keys
5. React app polls `/api/state` endpoint every 1 second
6. Updates UI components with new data

## Troubleshooting Guide

**Port 10000 not open:**
→ Ask user to start Azurite extension

**Agent throttled messages:**
→ Review `COST_CONTROL.md` and `UsageTracker` configuration

**No thoughts/actions appearing:**
→ Check `StateManager.AddThought()` and short-term memory storage
→ Verify `AgentStateService` is reading from short-term memory correctly
→ Check React app is successfully polling `/api/state`

**Agent has no memory of previous iterations:**
→ Check `LoadRecentMemoriesAsync()` is being called at startup
→ Verify long-term memory service is properly configured
→ Review memory refresh logic (every 10 iterations or hourly)
→ Check that `BuildMemoryContext()` is formatting memories correctly

**React app connection errors:**
→ Verify ALAN.ChatApi is running on port 5001
→ Check `VITE_API_URL` in `.env` file
→ Review CORS configuration in ChatApi

**Azure OpenAI authentication errors:**
→ Verify managed identity and endpoint configuration

## Best Practices

### Architecture and code Best Practices

#### Docker and Deployment

- **Use multi-stage builds** - Separate build and runtime stages for smaller images
- **Layer caching** - Order COPY commands to maximize cache hits
- **Production readiness** - Set appropriate environment variables and expose correct ports
- **Health checks** - Implement health check endpoints for container orchestration
- **Next.js standalone output** - Use `output: 'standalone'` in next.config.ts for Docker deployment

#### Frontend Development (Next.js)

- **Server and Client Components** - Use Server Components by default, Client Components only when needed
- **TypeScript strict mode** - Enable strict type checking
- **Environment variables** - Use `NEXT_PUBLIC_` prefix for client-side variables
- **API routes** - Use Next.js API routes sparingly, prefer dedicated backend services
- **Rewrites** - Configure rewrites in next.config.ts for API proxying
- **Commit package-lock.json** - Always track package-lock.json for reproducible builds (only ignore for npm libraries)

#### Security

- **Use managed identity** for Azure service authentication (avoid connection strings with secrets)
- **Validate all inputs** - Sanitize user inputs, especially in `HumanInputHandler`
- **Use parameterized queries** - Prevent injection attacks in any data access
- **Implement proper authorization** - Verify permissions before sensitive operations
- **Avoid logging sensitive data** - Never log tokens, secrets, or PII
- **Use secure defaults** - HTTPS, secure cookies, proper CORS configuration

#### Reliability

- **Implement retry policies** - Use Polly for transient fault handling with Azure services
- **Use circuit breaker patterns** - Prevent cascading failures in distributed systems
- **Handle exceptions gracefully** - Use try-catch with specific exception types
- **Implement health checks** - Monitor service health for `ALAN.Agent` and `ALAN.Web`
- **Use cancellation tokens** - Support graceful shutdown in background services
- **Validate configuration** - Fail fast on missing required settings at startup

#### Resiliency

ALAN implements comprehensive resiliency patterns using **Polly v8.5.0** for all Azure service integrations. All external API calls are wrapped with retry policies featuring exponential backoff and jitter.

**Resilience Implementation:**

- **Use ResiliencePolicy helper** - Located in `src/ALAN.Shared/Services/Resilience/ResiliencePolicy.cs`
- **Storage operations** - 3 retries, 1s initial delay, ~7s max (handles 429, 503, 504, 408)
- **OpenAI operations** - 5 retries, 2s initial delay, ~62s max (handles 429, 503, 504, 500)
- **Integrated services** - AzureBlobShortTermMemoryService, AzureBlobLongTermMemoryService, AzureStorageQueueService, AutonomousAgent
- **Proper cancellation** - OperationCanceledException is NOT retried, allows immediate cancellation

**When adding new Azure service calls:**

```csharp
// Initialize pipeline in constructor
private readonly ResiliencePipeline _resiliencePipeline;

public MyService(ILogger<MyService> logger)
{
    _resiliencePipeline = ResiliencePolicy.CreateStorageRetryPipeline(logger);
}

// Wrap all external calls
public async Task<Data> GetDataAsync(CancellationToken ct)
{
    return await _resiliencePipeline.ExecuteAsync(async ct =>
        await _azureClient.GetAsync(ct), ct);
}
```

**General Resiliency Principles:**

- **Design for failure** - Assume Azure services may be temporarily unavailable
- **Implement idempotency** - State changes should be safe to retry
- **Use appropriate timeouts** - Prevent indefinite waits on external calls
- **Log meaningful context** - Include correlation IDs for distributed tracing
- **Graceful degradation** - UI should work with SignalR fallback to polling

**Testing Resiliency:**

- Test retry on transient errors (429, 503, 504, 408)
- Test success after retries
- Test failure after max retries
- Test no retry on non-transient errors (404, 401)
- Test proper cancellation token propagation
- Verify retry loop stops immediately on cancellation

For detailed documentation, see `docs/RESILIENCY.md`.

#### Code Style

- **Use `async/await` properly** - Avoid `.Result` and `.Wait()` (deadlock risk)
- **Dispose resources** - Use `using` statements or implement `IAsyncDisposable`
- **Prefer immutability** - Use `record` types for DTOs, `readonly` for fields
- **Use nullable reference types** - Enable `#nullable enable` and handle nulls explicitly
- **Follow naming conventions** - PascalCase for public members, camelCase for private
- **Keep methods focused** - Single responsibility, under 30 lines when possible
- **Use dependency injection** - Register services in `Program.cs`, avoid `new` in classes

#### Modern C# Features (C# 12+)

**Collection Expressions** - Use `[]` syntax for all collection initialization:

```csharp
// ✅ Preferred - Modern collection expressions
string[] vowels = ["a", "e", "i", "o", "u"];
List<int> numbers = [1, 2, 3, 4, 5];
IEnumerable<string> names = ["Alice", "Bob", "Charlie"];
Span<int> span = [1, 2, 3];

// ❌ Avoid - Old syntax
string[] vowels = new[] { "a", "e", "i", "o", "u" };
var numbers = new List<int> { 1, 2, 3, 4, 5 };
```

**Spread Operator** - Use `..` to expand collections inline:

```csharp
// ✅ Preferred - Spread operator
int[] row1 = [1, 2, 3];
int[] row2 = [4, 5, 6];
int[] combined = [..row1, ..row2, 7, 8];

// Conditional spreading
bool includeExtra = true;
int[] result = [..numbers, ..includeExtra ? [99, 100] : []];

// ❌ Avoid - Manual concatenation
var combined = row1.Concat(row2).Append(7).Append(8).ToArray();
```

**Primary Constructors** - Use for dependency injection and simple initialization:

```csharp
// ✅ Preferred - Primary constructor
public class ExampleService(ILogger<ExampleService> logger, IConfiguration config)
{
    public void DoWork() => logger.LogInformation("Working with {Config}", config);
}

// ❌ Avoid - Traditional constructor with field assignment
public class ExampleService
{
    private readonly ILogger<ExampleService> _logger;
    private readonly IConfiguration _config;

    public ExampleService(ILogger<ExampleService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }
}
```

**Collection Expressions Best Practices**:

- Use `[]` for empty collections instead of `new List<T>()` or `Array.Empty<T>()`
- Prefer collection expressions for all collection types (arrays, lists, spans, IEnumerable)
- Use spread `..` to combine collections efficiently
- Leverage target-typing - no need to specify type when it's inferred

**When to Use Each Feature**:

- **Collection expressions**: Any time you initialize a collection with values
- **Spread operator**: Combining multiple collections, conditional element inclusion
- **Primary constructors**: Dependency injection, immutable data classes, simple initialization
- **`required` properties**: Force property initialization without constructor parameters

#### Architecture Patterns

- **Clean separation of concerns** - Agent logic, Web UI, and Shared models are separate projects
- **Repository pattern** - `IShortTermMemoryService` and `ILongTermMemoryService` abstract storage
- **Event-driven updates** - Use events for state changes (see `StateManager`)
- **Background services** - Use `IHostedService` for long-running operations
- **Options pattern** - Configure services via `IOptions<T>` from `appsettings.json`

#### Reference Documentation

- [Azure Well-Architected Framework](https://learn.microsoft.com/azure/well-architected/)
- [.NET Application Architecture](https://learn.microsoft.com/dotnet/architecture/)
- [Semantic Kernel Documentation](https://learn.microsoft.com/semantic-kernel/)
- [ASP.NET Core Security Best Practices](https://learn.microsoft.com/aspnet/core/security/)

### Memory System

**ALWAYS maintain knowledge continuity:**

- Agent must load memories at startup via `LoadRecentMemoriesAsync()`
- Include memory context in all AI prompts (see `ThinkAndActAsync()`)
- Refresh memories periodically (current: every 10 iterations or hourly)
- Never overwrite memories - only append via `StoreMemoryAsync()`

**When modifying the agent loop:**

- Ensure memory loading happens before first iteration
- Include `BuildMemoryContext()` output in prompts
- Maintain the importance + recency weighting system
- Respect the additive-only memory pattern

**When adding new memory types:**

- Update `LoadRecentMemoriesAsync()` to include the new type
- Adjust weights in `BuildMemoryContext()` grouping logic
- Consider how the type affects importance calculations
- Document any new memory patterns

### Future Multi-Agent Considerations

The architecture is prepared for multiple specialized agents:

- Interface-based memory services enable shared or isolated stores
- Memory tagging supports agent-specific filtering
- MCP integration pattern allows agent-specific tools
- Additive memory design prevents conflicts

## Testing Requirements

### Test Suite Overview

The project uses **xUnit** with **Moq** for testing. All new features and modifications **must** include corresponding unit tests.

**Test Projects:**

| Project            | Location                    | Coverage                                                           |
| ------------------ | --------------------------- | ------------------------------------------------------------------ |
| ALAN.Agent.Tests   | `tests/ALAN.Agent.Tests/`   | UsageTracker, StateManager, CodeProposalService, AutonomousAgent   |
| ALAN.Shared.Tests  | `tests/ALAN.Shared.Tests/`  | AgentState, AgentThought, AgentAction, CodeProposal, Memory models |
| ALAN.ChatApi.Tests | `tests/ALAN.ChatApi.Tests/` | AgentStateService, Controllers (State, HumanInput, CodeProposal)   |

**Test Files:**

- `tests/ALAN.Agent.Tests/Services/` - Core agent services tests
- `tests/ALAN.Shared.Tests/Models/` - All shared model tests
- `tests/ALAN.Shared.Tests/Services/` - Shared service tests
- `tests/ALAN.ChatApi.Tests/Services/` - ChatApi service tests
- `tests/ALAN.ChatApi.Tests/Controllers/` - API controller tests

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/ALAN.Agent.Tests/ALAN.Agent.Tests.csproj

# Run with detailed output
dotnet test --verbosity normal
```

### Testing Guidelines

1. **Every new feature must have tests** - No PR should be merged without test coverage
2. **Every bug fix must include a regression test** - Prevent the bug from recurring
3. **Follow AAA pattern** - Arrange, Act, Assert structure in all tests
4. **Use descriptive test names** - `MethodName_Scenario_ExpectedBehavior`
5. **Mock external dependencies** - Use Moq for services, storage, and Azure clients
6. **Test edge cases** - Include boundary conditions and error scenarios
7. **Keep tests fast** - Unit tests should run in milliseconds

For detailed test documentation, see `TEST_SUITE_SUMMARY.md`.

## Docker and Deployment

### Dockerfiles

- **Dockerfile.agent** - Builds ALAN.Agent service
- **Dockerfile.chatapi** - Builds ALAN.ChatApi service
- **Dockerfile.web** - Builds Next.js frontend with standalone output

### Building Docker Images

```bash
# Build all services
docker-compose build

# Build specific service
docker build -f Dockerfile.web -t alan-web .
docker build -f Dockerfile.chatapi -t alan-chatapi .
docker build -f Dockerfile.agent -t alan-agent .
```

### Running with Docker Compose

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down
```

### Environment Variables for Docker

Set these in `.env` file or pass to docker-compose:

```env
AZURE_OPENAI_ENDPOINT=https://your-instance.openai.azure.com/
AZURE_OPENAI_API_KEY=your-key-here
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
AGENT_MAX_LOOPS_PER_DAY=4000
AGENT_MAX_TOKENS_PER_DAY=8000000
```

## Azure Infrastructure Deployment

ALAN includes comprehensive Bicep templates for deploying to Azure Container Apps with production-ready infrastructure.

### Infrastructure Overview

The `infra/` directory contains Infrastructure as Code (IaC) templates following Azure Developer CLI (azd) conventions:

**Core Files:**

- `main.bicep` - Entry point with subscription-level deployment and default parameters
- `main.parameters.json` - Parameters file supporting environment variables (e.g., `${AZURE_ENV_NAME}`)
- `resources.bicep` - Main resource deployment orchestrating all Azure resources
- `abbreviations.json` - Azure resource naming conventions
- `modules/container-app.bicep` - Reusable Container App module

**Deployed Resources:**

1. **Virtual Network** with three subnets (infrastructure, private endpoints, container apps)
2. **Azure Storage Account** (private) with blob containers and queues
3. **Azure OpenAI** (private) with GPT-4o-mini deployment
4. **Container Registry** (private) for container images
5. **Container Apps Environment** with VNet integration
6. **Three Container Apps**: agent (internal), chatapi (internal), web (public)
7. **Managed Identity** for secure Azure resource access
8. **Log Analytics Workspace** for monitoring
9. **Private DNS Zones** for private endpoint resolution

### Deployment Options

**Using Azure Developer CLI (azd) - Recommended:**

```bash
# Initialize environment
azd init

# Set required parameters
azd env set AZURE_ENV_NAME dev
azd env set AZURE_LOCATION eastus
azd env set AZURE_PRINCIPAL_ID $(az ad signed-in-user show --query id -o tsv)

# Provision infrastructure
azd provision

# Build and deploy applications
azd deploy
```

**Using Azure CLI:**

```bash
# Deploy at subscription level
az deployment sub create \
  --location eastus \
  --template-file ./infra/main.bicep \
  --parameters ./infra/main.parameters.json \
  --parameters environmentName=dev location=eastus
```

### Security Architecture

- **Private Endpoints**: Storage and OpenAI accessible only through private endpoints
- **Network Isolation**: All resources deployed in VNet with controlled access
- **Managed Identity Authentication**: No connection strings or keys in configuration
- **Public Access**: Only the web application has public ingress; agent and chatapi are internal
- **Private DNS Zones**: Automatic DNS resolution for private endpoints

### Configuration Parameters

**Required:**

- `environmentName` - Environment name (dev, staging, prod)
- `location` - Azure region (eastus, westus2, etc.)

**Optional Reliability Features:**

- `enableZoneRedundancy` - Enable zone redundancy for production (default: false)
- `enableAutoScaling` - Enable Container Apps auto-scaling (default: false)
- `minReplicas` - Minimum replica count (default: 1)
- `maxReplicas` - Maximum replica count when auto-scaling (default: 10)

**Application Settings:**

- `openAiDeploymentName` - OpenAI deployment name (default: gpt-4o-mini)
- `openAiModelName` - OpenAI model name (default: gpt-4o-mini)
- `agentMaxLoopsPerDay` - Maximum agent loops per day (default: 4000)
- `agentMaxTokensPerDay` - Maximum tokens per day (default: 8000000)

### Deployment Outputs

After deployment, use outputs for local development:

```bash
# Get all outputs with azd
azd env get-values

# Get specific output with Azure CLI
az deployment group show \
  --resource-group rg-alan-dev \
  --name resources-dev \
  --query properties.outputs.AZURE_OPENAI_ENDPOINT.value
```

**Key Outputs:**

- `AZURE_OPENAI_ENDPOINT` - Set in local `.env`
- `AZURE_STORAGE_CONNECTION_STRING` - Set in local `.env`
- `WEB_APP_URL` - Public web application URL
- `AZURE_MANAGED_IDENTITY_CLIENT_ID` - For managed identity testing

### Using Azure Verified Modules (AVM)

The infrastructure uses AVM modules for these resources:

- `br/public:avm/res/managed-identity/user-assigned-identity` - Managed Identity
- `br/public:avm/res/operational-insights/workspace` - Log Analytics
- `br/public:avm/res/network/virtual-network` - Virtual Network
- `br/public:avm/res/storage/storage-account` - Storage Account
- `br/public:avm/res/network/private-dns-zone` - Private DNS Zones
- `br/public:avm/res/cognitive-services/account` - Azure OpenAI
- `br/public:avm/res/container-registry/registry` - Container Registry
- `br/public:avm/res/app/managed-environment` - Container Apps Environment

AVM modules follow Microsoft best practices and are maintained by the Azure team.

### CI/CD Integration

**GitHub Actions:**

- Security scanning workflow at `.github/workflows/security-scan.yml`
- Automated Checkov security validation on infrastructure changes
- Configure with azd workflows using `azure.yaml`
- Supports automatic deployment on push to main/develop branches

### Building Container Images for Azure

After infrastructure deployment, build and push images to Azure Container Registry:

```bash
# Login to ACR
az acr login --name <registry-name>

# Option 1: Build locally and push
docker build -f Dockerfile.agent -t <registry>.azurecr.io/alan-agent:latest .
docker push <registry>.azurecr.io/alan-agent:latest

docker build -f Dockerfile.chatapi -t <registry>.azurecr.io/alan-chatapi:latest .
docker push <registry>.azurecr.io/alan-chatapi:latest

docker build -f Dockerfile.web -t <registry>.azurecr.io/alan-web:latest .
docker push <registry>.azurecr.io/alan-web:latest

# Option 2: Use ACR build tasks (recommended)
az acr build --registry <registry> --image alan-agent:latest -f Dockerfile.agent .
az acr build --registry <registry> --image alan-chatapi:latest -f Dockerfile.chatapi .
az acr build --registry <registry> --image alan-web:latest -f Dockerfile.web .
```

### Cost Estimation

**Development Environment (~$100-300/month):**

- Container Apps (3 apps, 0.5 vCPU, 1GB each): ~$30-50
- Storage Account (LRS): ~$5-10
- Azure OpenAI (gpt-4o-mini, 100K TPM): ~$50-200 (usage-based)
- Virtual Network: ~$0
- Log Analytics: ~$10-20
- Container Registry (Basic): ~$5

**Production Environment** with zone redundancy and auto-scaling will cost more.

### Infrastructure Best Practices

1. **Use managed identity** - Avoid connection strings with secrets in production
2. **Enable zone redundancy** - For production deployments (`enableZoneRedundancy=true`)
3. **Configure auto-scaling** - Based on workload patterns (`enableAutoScaling=true`)
4. **Monitor costs** - Set up Azure Cost Management alerts
5. **Review logs** - Use Log Analytics for troubleshooting and monitoring
6. **Keep images updated** - Regularly rebuild and push container images
7. **Test in dev first** - Always validate infrastructure changes in development
8. **Use private endpoints** - Secure access to Azure services
9. **Follow naming conventions** - Use `abbreviations.json` for resource names
10. **Use Checkov scans for security** - Integrate into CI/CD pipelines and use it locally with `uv run checkov` or use `LOG_LEVEL=DEBUG uv run checkov` for detailed output.

### Troubleshooting Infrastructure

**Container App not starting:**

- Check Container Apps logs in Azure Portal
- Verify managed identity has Storage and OpenAI permissions
- Ensure container images are pushed to ACR
- Validate environment variables are set correctly

**Private endpoint issues:**

- Verify private endpoints are in "Approved" state
- Check Private DNS zones are linked to VNet
- Ensure Container Apps subnet has correct delegations

**Access denied errors:**

- Verify managed identity role assignments on Storage and OpenAI
- Check AZURE_CLIENT_ID environment variable is set in Container Apps
- Ensure network access is allowed from Container Apps subnet

For detailed infrastructure documentation, see `infra/README.md`.

## Quick Reference

- **Agent Loop Interval**: 5 seconds (configurable in `AutonomousAgent`)
- **Memory Refresh**: Every 10 iterations OR every 1 hour (whichever comes first)
- **Memory Context Size**: Top 20 most relevant memories (importance × 0.7 + recency × 0.3)
- **Cost Limits**: See `UsageTracker` (default: 4000 loops/day, 8M tokens/day)
- **Storage**: Azurite on port 10000 (local), Azure Blob Storage (production)
- **Short-term Memory TTL**: 8 hours for thoughts/actions, 1 hour for agent state
- **Memory Consolidation**: Runs every 6 hours to promote important items to long-term storage
- **UI Updates**: React app polls `/api/state` every 1 second
- **React Dev Server**: Port 5269 (Vite)
- **ChatApi**: Port 5001
- **Test Framework**: xUnit 2.9.3 with Moq 4.20.72

For detailed setup instructions, see `QUICKSTART.md`.
For React migration details, see `docs/REACT_MIGRATION.md`.

## Final remarks

When changing the codebase, ensure that these instructions are updated accordingly to reflect any new debugging steps or architecture changes.

**Remember**: All code changes require accompanying tests. Follow security, reliability, and resiliency best practices in every modification.
