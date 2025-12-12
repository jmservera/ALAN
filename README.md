# ALAN
Autonomous Learning Agent Network

ALAN is a Semantic Kernel-based autonomous agent solution that runs continuously on Azure. The agent can be observed in real-time through a web interface that displays its thoughts and actions, and can be steered via a prompt interface.

## Features

- **Autonomous Operation**: The agent runs in an infinite loop, continuously thinking and taking actions
- **Self-Improvement**: Batch learning process that consolidates memories and extracts learnings
- **Memory Architecture**: Short-term and long-term memory with automatic consolidation
- **Observable**: Real-time web interface showing agent thoughts, actions, and current state
- **Steerable**: Update the agent's directive through prompt configuration and REST API
- **Human Steering**: REST API for controlling agent behavior (pause, resume, update goals)
- **Azure-Ready**: Deployable to Azure App Service with included deployment templates
- **Real-time Updates**: Uses SignalR for live updates from the agent to the web interface
- **Batch Learning**: Periodic consolidation of memories into actionable insights
- **Resilient Architecture**: Automatic retry with exponential backoff for all Azure service calls

## Architecture

The solution consists of three main components:

1. **ALAN.Agent**: A background service that runs the autonomous agent using Semantic Kernel
2. **ALAN.Web**: An ASP.NET Core web application with SignalR for real-time observability
3. **ALAN.Shared**: Shared models and contracts between agent and web interface

### Memory System

ALAN includes a sophisticated memory architecture:
- **Short-term Memory**: Working memory for current context (in-memory or Redis)
- **Long-term Memory**: Persistent storage for experiences (in-memory or Azure AI Search)
- **Memory Consolidation**: Batch process that extracts learnings from memories
- **Automatic Cleanup**: Removes outdated low-importance memories

See [Memory Architecture Documentation](docs/MEMORY_ARCHITECTURE.md) for details.

### Human Steering

Control the agent through REST API endpoints:
- Update agent prompts/directives
- Pause/resume execution
- Trigger batch learning manually
- Query current state

See [Human Steering API Documentation](docs/HUMAN_STEERING_API.md) for details.

### Resiliency

ALAN implements comprehensive resiliency patterns:
- **Automatic Retry**: Exponential backoff for transient failures
- **Throttling Management**: Handles rate limits gracefully
- **Circuit Breaker**: Prevents cascading failures
- **Detailed Logging**: Track retry attempts and service health

See [Resiliency Documentation](docs/RESILIENCY.md) for details.

## Prerequisites

- .NET 8.0 SDK or later
- Azure OpenAI API key (or OpenAI API key)
- Docker (optional, for containerized deployment)
- Azure subscription (for cloud deployment)

## Configuration

### Required: Azure OpenAI

Set your Azure OpenAI configuration:

```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-api-key-here"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini"
```

Or in `src/ALAN.Agent/appsettings.json`:
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-api-key-here",
    "DeploymentName": "gpt-4o-mini"
  }
}
```

### Optional: Usage Limits

Configure cost controls (default shown):

```bash
export AGENT_MAX_LOOPS_PER_DAY="4000"
export AGENT_MAX_TOKENS_PER_DAY="8000000"
```

## Running Locally

### Using .NET CLI

1. Restore client-side libraries (first time only):
   ```bash
   cd src/ALAN.Web
   dotnet tool install -g Microsoft.Web.LibraryManager.Cli
   libman restore
   ```

2. Run the agent:
   ```bash
   cd src/ALAN.Agent
   dotnet run
   ```

3. Run the web interface (in a separate terminal):
   ```bash
   cd src/ALAN.Web
   dotnet run
   ```

4. Open your browser to `https://localhost:5001` (or the URL shown in the terminal)

### Using Docker Compose

```bash
OPENAI_API_KEY="your-api-key-here" docker-compose up
```

Then open your browser to `http://localhost:8080`

## Deploying to Azure

### Option 1: Using Azure ARM Template

1. Create a resource group:
   ```bash
   az group create --name rg-alan --location eastus
   ```

2. Deploy the template:
   ```bash
   az deployment group create \
     --resource-group rg-alan \
     --template-file .azure/deploy.json \
     --parameters openAiApiKey="your-api-key-here"
   ```

### Option 2: Manual Deployment

1. Build Docker images:
   ```bash
   docker build -f Dockerfile.web -t alan-web .
   docker build -f Dockerfile.agent -t alan-agent .
   ```

2. Push images to Azure Container Registry or Docker Hub

3. Create Azure App Service instances and configure them to use your container images

## Observability Features

The web interface provides real-time visibility into:

- **Agent Status**: Current state (Idle, Thinking, Acting, Paused, Error)
- **Current Goal**: What the agent is currently working on
- **Recent Thoughts**: Stream of agent's reasoning, planning, and reflections
- **Recent Actions**: Actions taken by the agent with their status and results
- **Connection Status**: SignalR connection health

## Customizing the Agent

You can customize the agent's behavior by:

1. Modifying the default prompt in `AutonomousAgent.cs`
2. Adjusting batch learning intervals in `BatchLearningService.cs`
3. Configuring memory retention policies
4. Implementing custom actions and skills
5. Adjusting the thinking loop interval
6. Using the Human Steering API to update directives at runtime

### Batch Learning Configuration

The batch learning process runs when:
- 100 iterations have passed (configurable)
- OR 4 hours have elapsed since the last batch

To change these thresholds, modify the `ShouldRunBatch` method in `BatchLearningService.cs`.

### Memory Configuration

Memory services can be swapped for production backends:
- Replace `InMemoryLongTermMemoryService` with Azure AI Search implementation
- Replace `InMemoryShortTermMemoryService` with Redis Cache implementation

## Project Structure

```
ALAN/
├── src/
│   ├── ALAN.Agent/          # Autonomous agent service
│   │   ├── Services/        # Agent implementation
│   │   └── Program.cs       # Entry point
│   ├── ALAN.Web/            # Web interface
│   │   ├── Hubs/            # SignalR hubs
│   │   ├── Pages/           # Razor pages
│   │   ├── Services/        # Background services
│   │   └── Program.cs       # Entry point
│   └── ALAN.Shared/         # Shared models
│       └── Models/          # Data models
├── .azure/                  # Azure deployment templates
├── Dockerfile.agent         # Agent container
├── Dockerfile.web           # Web container
└── docker-compose.yml       # Local development
```

## Technology Stack

- **Semantic Kernel**: AI orchestration framework
- **ASP.NET Core**: Web framework
- **SignalR**: Real-time communication
- **OpenAI GPT**: Language model
- **Docker**: Containerization
- **Azure App Service**: Cloud hosting

## License

This project is provided as-is for educational and demonstration purposes.

