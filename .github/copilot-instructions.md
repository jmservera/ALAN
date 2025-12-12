# Copilot Instructions for ALAN Project

## Project Overview

ALAN (Autonomous Learning Agent Network) is a Semantic Kernel-based autonomous agent solution that demonstrates continuous AI agent operation with real-time observability.

### Architecture Components

1. **ALAN.Agent** (Main Process)

   - Background service running an autonomous agent loop
   - Uses Semantic Kernel with Azure OpenAI for AI capabilities
   - Persists thoughts and actions to Azure Blob Storage (via Azurite locally)
   - Implements cost control through `UsageTracker`
   - Core logic in `AutonomousAgent.cs`

2. **ALAN.Web** (UI)

   - ASP.NET Core web application with SignalR
   - Reads execution data from storage and displays in real-time
   - Provides observability into agent thoughts, actions, and status
   - Entry point: `Program.cs`

3. **ALAN.Shared**
   - Shared models between agent and web interface
   - Contains `AgentState.cs`, `AgentThought.cs`, `AgentAction.cs`

## Running and Debugging

### Prerequisites

- .NET 8.0 SDK
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

2. **Restore client-side libraries (first time only)**
   ```bash
   cd src/ALAN.Web
   dotnet tool install -g Microsoft.Web.LibraryManager.Cli
   libman restore
   ```

3. **Set environment variables**

   - Copy values from `.env` to your environment
   - Required: `AZURE_OPENAI_ENDPOINT`, managed identity credentials
   - Required: `AZURE_STORAGE_CONNECTION_STRING` for Azure Blob Storage (Azurite locally)
      - Local storage uses Azurite with the default development storage connection string `DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;`

4. **Ensure Azurite is running**

   - Verify port 10000 is open (DO NOT restart it)
   - If not, ask user to start Azurite extension or
   - If running in a headless environment, ensure Azurite is installed (`npm install -g azurite`) and started manually.

5. **Launch both services**
   - Use the "ALAN (Agent + Web)" compound launch configuration
   - Or run individually:
     - "Launch Agent" - Starts `ALAN.Agent/Program.cs`
     - "Launch Web" - Starts `ALAN.Web/Program.cs`

### Common Debugging Scenarios

**Agent Loop Issues:**

- Check `AutonomousAgent.RunAsync()` infinite loop
- Verify `UsageTracker` isn't throttling (see `COST_CONTROL.md`)
- Look at `StateManager` for state persistence

**Storage Connection Problems:**

- Verify Azurite is running on port 10000 (DO NOT restart it)
- Check connection string in environment variables
- Review blob storage files in `__blobstorage__` directory

**Web UI Not Updating:**

- Check SignalR connection in browser console
- Verify `AgentHub` is receiving events
- Review `AgentStateService` polling logic

**Azure OpenAI Connection:**

- Ensure managed identity has access to Azure OpenAI
- Verify endpoint configuration in `Program.cs`
- Check deployment name matches configuration

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
3. **`TryRestorePreviousStateAsync()` attempts to restore previous execution state** from short-term memory at startup
4. **`LoadRecentMemoriesAsync()` loads accumulated knowledge** from long-term storage at startup
5. `UsageTracker.CanExecuteLoop()` checks daily limits
6. `ThinkAndActAsync()` generates thoughts and actions **with memory context** from previous iterations
7. `StateManager` stores thoughts/actions to **short-term memory only** (8-hour TTL)
8. **Memory refresh** occurs every 10 iterations or hourly to keep context current
9. `MemoryConsolidationService.ConsolidateShortTermMemoryAsync()` runs every 6 hours to:
   - Read thoughts and actions from short-term memory
   - Evaluate importance of each item
   - Promote important items (importance ≥ 0.5) to long-term memory with "consolidated" tag
   - Extract learnings from consolidated memories

### State Restoration on Startup

The agent automatically resumes from previous state when restarted:
- **State Source**: Reads `agent:current-state` from short-term memory (1-hour TTL)
- **Age Validation**: Rejects state older than 24 hours (configurable via `STATE_MAX_AGE_HOURS`)
- **Error State Handling**: Ignores previous states with `Error` status to ensure clean restart
- **Restoration Process**: Restores `CurrentGoal` and `CurrentPrompt` if state is valid
- **Resilience**: Gracefully handles corrupt or missing state by starting fresh
- **Logging**: Detailed logs for restoration success, rejection reasons, and failures

Configuration constant in `AutonomousAgent.cs`:
- `STATE_MAX_AGE_HOURS = 24` - Maximum age of state to restore (hours)

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

### Web Update Flow

1. `AgentStateService` polls **short-term memory** every 500ms
2. Retrieves current state from `agent:current-state` key
3. Retrieves thoughts from `thought:*` keys
4. Retrieves actions from `action:*` keys
5. `AgentHub` broadcasts via SignalR
6. `Index.cshtml` updates UI in real-time

## Troubleshooting Guide

**Port 10000 not open:**
→ Ask user to start Azurite extension

**Agent throttled messages:**
→ Review `COST_CONTROL.md` and `UsageTracker` configuration

**No thoughts/actions appearing:**
→ Check `StateManager.AddThought()` and short-term memory storage
→ Verify `AgentStateService` is reading from short-term memory correctly

**Agent has no memory of previous iterations:**
→ Check `LoadRecentMemoriesAsync()` is being called at startup
→ Verify long-term memory service is properly configured
→ Review memory refresh logic (every 10 iterations or hourly)
→ Check that `BuildMemoryContext()` is formatting memories correctly

**Agent not resuming from previous state:**
→ Check if state exists in short-term memory (`agent:current-state` key)
→ Verify state age is within 24-hour threshold (check `STATE_MAX_AGE_HOURS`)
→ Ensure previous state didn't have `Error` status (these are intentionally skipped)
→ Review logs for "Restored previous state" or rejection reasons
→ Check short-term memory TTL (1 hour for state) hasn't expired

**SignalR connection failed:**
→ Falls back to polling mode automatically (see `Index.cshtml`)

**Azure OpenAI authentication errors:**
→ Verify managed identity and endpoint configuration

## Best Practices

### Architecture and code Best Practices

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

- **Design for failure** - Assume Azure services may be temporarily unavailable
- **Implement idempotency** - State changes should be safe to retry
- **Use appropriate timeouts** - Prevent indefinite waits on external calls
- **Log meaningful context** - Include correlation IDs for distributed tracing
- **Graceful degradation** - UI should work with SignalR fallback to polling

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

| Project           | Location                   | Coverage                                                           |
| ----------------- | -------------------------- | ------------------------------------------------------------------ |
| ALAN.Agent.Tests  | `tests/ALAN.Agent.Tests/`  | UsageTracker, StateManager, CodeProposalService                    |
| ALAN.Shared.Tests | `tests/ALAN.Shared.Tests/` | AgentState, AgentThought, AgentAction, CodeProposal, Memory models |
| ALAN.Web.Tests    | `tests/ALAN.Web.Tests/`    | AgentStateService                                                  |

**Test Files:**

- `tests/ALAN.Agent.Tests/Services/UsageTrackerTests.cs` - Cost control and throttling
- `tests/ALAN.Agent.Tests/Services/StateManagerTests.cs` - State persistence and events
- `tests/ALAN.Agent.Tests/Services/CodeProposalServiceTests.cs` - Proposal workflow
- `tests/ALAN.Shared.Tests/Models/` - All shared model tests
- `tests/ALAN.Web.Tests/Services/AgentStateServiceTests.cs` - Web service tests

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

## Quick Reference

- **Agent Loop Interval**: 5 seconds (configurable in `AutonomousAgent`)
- **State Restoration**: Automatically attempts to resume from previous state (max 24 hours old)
- **Memory Refresh**: Every 10 iterations OR every 1 hour (whichever comes first)
- **Memory Context Size**: Top 20 most relevant memories (importance × 0.7 + recency × 0.3)
- **Cost Limits**: See `UsageTracker` (default: 4000 loops/day, 8M tokens/day)
- **Storage**: Azurite on port 10000 (local), Azure Blob Storage (production)
- **Short-term Memory TTL**: 8 hours for thoughts/actions, 1 hour for agent state
- **Memory Consolidation**: Runs every 6 hours to promote important items to long-term storage
- **UI Updates**: SignalR with polling fallback every 5 seconds
- **Test Framework**: xUnit 2.9.3 with Moq 4.20.72

For detailed setup instructions, see `QUICKSTART.md`.

## Final remarks

When changing the codebase, ensure that these instructions are updated accordingly to reflect any new debugging steps or architecture changes.

**Remember**: All code changes require accompanying tests. Follow security, reliability, and resiliency best practices in every modification.
