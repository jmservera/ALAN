# ALAN - Autonomous Learning Agent Network

An autonomous AI system capable of improving itself over time using Semantic Kernel and Azure services.

## Overview

ALAN (Autonomous Learning Agent Network) is a self-improving AI agent that:
- Runs autonomously in an infinite loop with safe pause/resume capability
- Maintains short-term and long-term memory using Azure services
- Periodically analyzes its own activity to create learnings
- Can analyze and propose improvements to its own codebase via GitHub integration
- Accepts human guidance through a REST API interface
- Ensures all self-modifications require human approval

## Architecture

### Core Components

1. **Autonomous Loop** (`ALAN.Core.Loop`)
   - Main infinite loop that runs continuously
   - Pause/resume capability for batch processing
   - Safe shutdown mechanism
   - Configurable iteration delays

2. **Memory Architecture** (`ALAN.Core.Memory`)
   - **Short-Term Memory**: In-memory cache for current context (last 1000 entries)
   - **Long-Term Memory**: Azure Blob Storage for persistent storage
   - **Learning Memory**: Summarized insights from batch processing

3. **Batch Learning Processor** (`ALAN.Core.BatchProcessing`)
   - Runs periodically (every N iterations)
   - Analyzes long-term memory entries
   - Uses Semantic Kernel to generate insights and learnings
   - Pauses main loop during processing

4. **Agent Orchestrator** (`ALAN.Core.Services`)
   - Plans and executes agent actions each iteration
   - Processes human input queue
   - Maintains context from memories and learnings
   - Uses AI to decide next actions

5. **GitHub Integration** (`ALAN.Core.GitHub`)
   - Reads repository code
   - Analyzes code for improvements
   - Creates pull requests (requires human approval)
   - Logs all reasoning to memory

6. **Human Steering API** (`ALAN.API`)
   - REST API for sending guidance to the agent
   - Endpoints to pause/resume/stop the agent
   - Query memory and learnings
   - Monitor agent status

## Getting Started

### Prerequisites

- .NET 10.0 or later
- Azure subscription (for OpenAI, Storage, and optionally Cognitive Search)
- GitHub account (optional, for self-improvement features)

### Configuration

Create an `appsettings.json` file (or use environment variables):

```json
{
  "Agent": {
    "Azure": {
      "OpenAIEndpoint": "https://your-resource.openai.azure.com/",
      "OpenAIKey": "your-api-key",
      "OpenAIDeploymentName": "gpt-4",
      "StorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
      "SearchEndpoint": "https://your-search.search.windows.net",
      "SearchKey": "your-search-key"
    },
    "GitHub": {
      "Token": "your-github-token",
      "RepositoryOwner": "your-username",
      "RepositoryName": "ALAN",
      "BranchName": "main",
      "EnableSelfImprovement": false
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

### Running the Agent

#### Console Mode

```bash
cd src/ALAN.Agent
dotnet run
```

The agent will start running autonomously. Press Ctrl+C to stop gracefully.

#### API Mode (with Human Steering Interface)

```bash
cd src/ALAN.API
dotnet run
```

The API will start on `http://localhost:5000` (or configured port) and the agent runs in the background.

### Human Steering API

#### Send guidance to the agent:
```bash
curl -X POST http://localhost:5000/api/agent/input \
  -H "Content-Type: application/json" \
  -d '{"input": "Focus on code quality improvements"}'
```

#### Get agent status:
```bash
curl http://localhost:5000/api/agent/status
```

#### Pause the agent:
```bash
curl -X POST http://localhost:5000/api/agent/pause
```

#### Resume the agent:
```bash
curl -X POST http://localhost:5000/api/agent/resume
```

#### Query memories:
```bash
curl http://localhost:5000/api/memory/short-term?limit=10
curl http://localhost:5000/api/memory/learnings
curl http://localhost:5000/api/memory/search?query=error
```

## Safety and Governance

### Human Approval Gates

All code changes proposed by ALAN:
- Are created as pull requests (never committed directly)
- Include detailed reasoning and safety checklist
- Require human review before merging
- Are logged to long-term memory

### Monitoring

All autonomous actions are:
- Logged with timestamps
- Stored in long-term memory
- Queryable via API
- Include reasoning context

### Safe Operations

- Loop can be paused/resumed without data loss
- Graceful shutdown on Ctrl+C or SIGTERM
- Batch processing pauses main loop
- Error handling with recovery

## Project Structure

```
ALAN/
├── src/
│   ├── ALAN.Core/              # Core libraries
│   │   ├── Memory/             # Memory services
│   │   ├── Loop/               # Autonomous loop
│   │   ├── BatchProcessing/    # Batch learning
│   │   ├── Services/           # Agent orchestrator
│   │   ├── GitHub/             # GitHub integration
│   │   └── Configuration/      # Configuration models
│   ├── ALAN.Agent/             # Console application
│   └── ALAN.API/               # REST API for human steering
└── README.md
```

## How It Works

### Main Loop Flow

1. Agent starts and enters infinite loop
2. Each iteration:
   - Checks for human input
   - Retrieves context from memories
   - Uses AI to plan next action
   - Executes action (monitor, analyze, reflect, etc.)
   - Logs results to memory
   - Waits configured delay
3. Every N iterations:
   - Pauses main loop
   - Runs batch learning process
   - Summarizes recent memories into learnings
   - Updates knowledge base
   - Resumes main loop

### Memory Flow

- **Short-term**: Current tasks, recent actions (in-memory)
- **Long-term**: All activities, decisions, errors (Azure Blob)
- **Learnings**: AI-generated insights from analysis (Azure Blob)

### Self-Improvement Flow

1. Agent analyzes repository code
2. Uses AI to identify improvements
3. Logs reasoning to memory
4. Creates pull request with changes
5. Waits for human approval
6. Learns from approval/rejection

## Development

### Build

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Add New Actions

Extend `ActionType` enum and `ExecuteActionAsync` method in `AgentOrchestrator.cs`.

### Customize Batch Processing

Modify `BatchLearningProcessor.cs` to change how memories are analyzed and summarized.

## Security Considerations

- **Never commit secrets**: Use environment variables or Azure Key Vault
- **Validate all inputs**: Human inputs are queued and validated
- **Code review required**: All AI-generated code changes need approval
- **Audit trail**: All actions logged to long-term memory
- **Rate limiting**: Consider adding rate limits for API endpoints
- **Access control**: Add authentication/authorization for production

## License

MIT License - See LICENSE file for details

## Contributing

Contributions welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Submit a pull request

## Support

For issues and questions:
- GitHub Issues: [Create an issue](https://github.com/jmservera/ALAN/issues)
- Documentation: See docs/ folder

## Roadmap

- [ ] Azure Cognitive Search integration for semantic memory search
- [ ] Multi-agent collaboration capabilities
- [ ] Plugin system for custom actions
- [ ] Web UI for monitoring and control
- [ ] Advanced code analysis with static analysis tools
- [ ] Integration with Microsoft Learn MCP server
- [ ] Reinforcement learning from human feedback
- [ ] Export/import memory snapshots
