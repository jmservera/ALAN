# Quick Start Guide

This guide will help you get ALAN up and running quickly for testing.

## Prerequisites

- .NET 10.0 SDK
- (Optional) Azure subscription for full features
- (Optional) GitHub account for self-improvement features

## Option 1: Run Without Azure (Development Mode)

This mode uses in-memory storage and no AI - perfect for testing the architecture.

### Step 1: Update Configuration

The default `appsettings.json` is already configured for development mode with no Azure services.

### Step 2: Run the Console Agent

```bash
cd src/ALAN.Agent
dotnet run
```

You should see:
```
=== ALAN - Autonomous Learning Agent Network ===
Initializing agent...

info: ALAN.Core.Loop.AutonomousLoop[0]
      Autonomous loop started
info: ALAN.Core.Loop.AutonomousLoop[0]
      Autonomous loop is running
```

The agent will run iterations every 30 seconds. Press Ctrl+C to stop.

### Step 3: Run the API (Optional)

In a separate terminal:

```bash
cd src/ALAN.API
dotnet run
```

The API will be available at `http://localhost:5000` (or the port shown in the console).

## Option 2: Run With Azure OpenAI (Recommended)

### Step 1: Set Up Azure Resources

1. Create an Azure OpenAI resource
2. Deploy a GPT-4 model
3. Get your endpoint and API key

### Step 2: Configure the Agent

Create `src/ALAN.Agent/appsettings.local.json`:

```json
{
  "Agent": {
    "Azure": {
      "OpenAIEndpoint": "https://your-resource.openai.azure.com/",
      "OpenAIKey": "your-api-key-here",
      "OpenAIDeploymentName": "gpt-4"
    }
  }
}
```

### Step 3: Run the Agent

```bash
cd src/ALAN.Agent
dotnet run
```

Now the agent will use AI to plan actions!

## Option 3: Full Production Setup

### Step 1: Set Up All Azure Resources

1. **Azure OpenAI**: For AI reasoning
2. **Azure Storage Account**: For long-term memory
3. **Azure Cognitive Search** (optional): For semantic search

### Step 2: Configure Everything

Create `src/ALAN.Agent/appsettings.local.json`:

```json
{
  "Agent": {
    "Azure": {
      "OpenAIEndpoint": "https://your-resource.openai.azure.com/",
      "OpenAIKey": "your-api-key",
      "OpenAIDeploymentName": "gpt-4",
      "StorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=youraccount;AccountKey=yourkey;EndpointSuffix=core.windows.net"
    },
    "Memory": {
      "UseLongTermMemory": true
    },
    "Loop": {
      "EnableBatchProcessing": true
    }
  }
}
```

### Step 3: Enable GitHub Integration (Optional)

1. Create a GitHub Personal Access Token with `repo` scope
2. Add to configuration:

```json
{
  "Agent": {
    "GitHub": {
      "Token": "github_pat_xxxxxxxxxxxxx",
      "RepositoryOwner": "yourusername",
      "RepositoryName": "ALAN",
      "EnableSelfImprovement": true
    }
  }
}
```

### Step 4: Run Everything

```bash
# Run the API (includes background agent)
cd src/ALAN.API
dotnet run
```

## Testing the Human Steering Interface

With the API running, you can control the agent:

### Send a Command

```bash
curl -X POST http://localhost:5000/api/agent/input \
  -H "Content-Type: application/json" \
  -d '{"input": "Focus on monitoring system health"}'
```

### Check Agent Status

```bash
curl http://localhost:5000/api/agent/status
```

Example response:
```json
{
  "isRunning": true,
  "isPaused": false,
  "iterationCount": 42,
  "timestamp": "2024-12-08T10:30:00Z"
}
```

### View Recent Memories

```bash
# Short-term memory
curl http://localhost:5000/api/memory/short-term?limit=5

# Learnings (if batch processing has run)
curl http://localhost:5000/api/memory/learnings

# Search memories
curl "http://localhost:5000/api/memory/search?query=error&limit=10"
```

### Control the Agent

```bash
# Pause
curl -X POST http://localhost:5000/api/agent/pause

# Resume
curl -X POST http://localhost:5000/api/agent/resume

# Stop
curl -X POST http://localhost:5000/api/agent/stop
```

## What Happens in Each Iteration?

1. Agent checks for human input from the queue
2. Retrieves recent context from short-term memory
3. Retrieves learnings from long-term memory
4. Uses AI (if configured) to plan next action
5. Executes the planned action:
   - `ANALYZE_CODE`: Analyze repository (if GitHub configured)
   - `MONITOR`: Check system health
   - `REFLECT`: Analyze recent activities
   - `IDLE`: No action
6. Logs results to memory
7. Waits 30 seconds (configurable)
8. Repeats

## Batch Processing

Every 100 iterations (configurable), the agent:

1. Pauses the main loop
2. Retrieves recent long-term memories
3. Uses AI to analyze and summarize
4. Creates "Learning" entries with insights
5. Resumes the main loop

## Monitoring Logs

The agent logs everything to console. In production, configure Application Insights:

```json
{
  "Logging": {
    "ApplicationInsights": {
      "LogLevel": {
        "Default": "Information"
      }
    }
  },
  "ApplicationInsights": {
    "InstrumentationKey": "your-key"
  }
}
```

## Troubleshooting

### "Cannot connect to Azure OpenAI"

- Check your endpoint URL (should end with `/`)
- Verify API key is correct
- Ensure the deployment name matches your Azure deployment

### "Agent not starting"

- Check logs for errors
- Verify configuration is valid JSON
- Ensure all required packages are restored (`dotnet restore`)

### "No actions being taken"

- This is normal without Azure OpenAI configured
- The agent will log "IDLE" actions
- Configure Azure OpenAI to enable AI-driven decisions

## Next Steps

1. **Monitor the agent** - Watch the logs to see what it's doing
2. **Send human input** - Guide the agent with specific instructions
3. **Enable GitHub integration** - Let it analyze your code
4. **Configure batch processing** - Let it learn from its activities
5. **Build custom actions** - Extend the agent with your own logic

## Safety Reminders

- All code changes require human approval (via PRs)
- The agent cannot make direct commits
- All actions are logged to memory
- You can pause/stop the agent anytime
- Human input always takes priority

## Example Session

```bash
# Terminal 1: Start the API
cd src/ALAN.API
dotnet run

# Terminal 2: Interact with the agent
# Send guidance
curl -X POST http://localhost:5000/api/agent/input \
  -H "Content-Type: application/json" \
  -d '{"input": "Please monitor system health"}'

# Wait a few seconds...

# Check recent memories
curl http://localhost:5000/api/memory/short-term?limit=5

# Pause to review
curl -X POST http://localhost:5000/api/agent/pause

# Review learnings (if available)
curl http://localhost:5000/api/memory/learnings

# Resume
curl -X POST http://localhost:5000/api/agent/resume
```

Enjoy exploring ALAN!
