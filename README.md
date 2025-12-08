# ALAN
Autonomous Learning Agent Network

ALAN is a Semantic Kernel-based autonomous agent solution that runs continuously on Azure. The agent can be observed in real-time through a web interface that displays its thoughts and actions, and can be steered via a prompt interface.

## Features

- **Autonomous Operation**: The agent runs in an infinite loop, continuously thinking and taking actions
- **Observable**: Real-time web interface showing agent thoughts, actions, and current state
- **Steerable**: Update the agent's directive through prompt configuration
- **Azure-Ready**: Deployable to Azure App Service with included deployment templates
- **Real-time Updates**: Uses SignalR for live updates from the agent to the web interface

## Architecture

The solution consists of three main components:

1. **ALAN.Agent**: A background service that runs the autonomous agent using Semantic Kernel
2. **ALAN.Web**: An ASP.NET Core web application with SignalR for real-time observability
3. **ALAN.Shared**: Shared models and contracts between agent and web interface

## Prerequisites

- .NET 8.0 SDK or later
- OpenAI API key (or Azure OpenAI)
- Docker (optional, for containerized deployment)
- Azure subscription (for cloud deployment)

## Configuration

Set your OpenAI API key in one of the following ways:

1. Environment variable:
   ```bash
   export OPENAI_API_KEY="your-api-key-here"
   ```

2. In `src/ALAN.Agent/appsettings.json`:
   ```json
   {
     "OpenAI": {
       "ApiKey": "your-api-key-here",
       "ModelId": "gpt-4o-mini"
     }
   }
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
2. Adding new plugins to the Semantic Kernel
3. Implementing custom actions and skills
4. Adjusting the thinking loop interval

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

