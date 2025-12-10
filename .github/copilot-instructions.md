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

2. **Set environment variables**

   - Copy values from `.env` to your environment
   - Required: `AZURE_OPENAI_ENDPOINT`, managed identity credentials
   - Local storage uses Azurite on `UseDevelopmentStorage=true`

3. **Launch both services**
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
3. `UsageTracker.CanExecuteLoop()` checks daily limits
4. `ThinkAndActAsync()` generates thoughts and actions
5. `StateManager` stores thoughts/actions to **short-term memory only** (8-hour TTL)
6. `MemoryConsolidationService.ConsolidateShortTermMemoryAsync()` runs every 6 hours to:
   - Read thoughts and actions from short-term memory
   - Evaluate importance of each item
   - Promote important items (importance ≥ 0.5) to long-term memory with "consolidated" tag
   - Extract learnings from consolidated memories

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

**SignalR connection failed:**
→ Falls back to polling mode automatically (see `Index.cshtml`)

**Azure OpenAI authentication errors:**
→ Verify managed identity and endpoint configuration

## Quick Reference

- **Agent Loop Interval**: 5 seconds (configurable in `AutonomousAgent`)
- **Cost Limits**: See `UsageTracker` (default: 4000 loops/day, 8M tokens/day)
- **Storage**: Azurite on port 10000 (local), Azure Blob Storage (production)
- **Short-term Memory TTL**: 8 hours for thoughts/actions, 1 hour for agent state
- **Memory Consolidation**: Runs every 6 hours to promote important items to long-term storage
- **UI Updates**: SignalR with polling fallback every 5 seconds

For detailed setup instructions, see `QUICKSTART.md`.

## Final remarks

When changing the codebase, ensure that these instructions are updated accordingly to reflect any new debugging steps or architecture changes.
