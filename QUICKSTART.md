# Quick Start Guide

This guide will help you get ALAN up and running quickly.

## Prerequisites

- .NET 8.0 SDK ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- OpenAI API Key ([Get one here](https://platform.openai.com/api-keys))
- Docker (optional, for containerized deployment)

## 5-Minute Setup

### 1. Clone and Configure

```bash
# Clone the repository
git clone https://github.com/jmservera/ALAN.git
cd ALAN

# Set your OpenAI API key
export OPENAI_API_KEY="sk-your-api-key-here"
```

### 2. Option A: Run with Docker Compose (Recommended)

```bash
docker-compose up
```

Then open your browser to: `http://localhost:8080`

### 3. Option B: Run Locally with .NET

**First time setup - restore client libraries:**
```bash
cd src/ALAN.Web
dotnet tool install -g Microsoft.Web.LibraryManager.Cli
libman restore
cd ../..
```

**Terminal 1 - Agent:**
```bash
cd src/ALAN.Agent
dotnet run
```

**Terminal 2 - ChatApi (Optional - for AG-UI support):**
```bash
cd src/ALAN.ChatApi
dotnet run
```

**Terminal 3 - Web Interface:**
```bash
cd src/ALAN.Web
# Build frontend (first time only, or when frontend code changes)
npm install
npm run build
# Run the web server
dotnet run
```

Then open your browser to: `https://localhost:5001` (or the URL shown in terminal)

> **Note**: The Chat page now uses CopilotKit for a standard AG-UI interface. You must build the frontend before running the web application (see `src/ALAN.Web/README.md` for details).

## What You'll See

The web interface shows:
- **Agent Status Panel**: Current state and goals
- **Thoughts Stream**: Real-time agent reasoning
- **Actions Panel**: What the agent is doing

## Customizing the Agent

### Change the Agent's Directive

Edit `src/ALAN.Agent/Services/AutonomousAgent.cs`, line 14:

```csharp
private string _currentPrompt = "Your custom directive here";
```

### Use Azure OpenAI Instead

Edit `src/ALAN.Agent/appsettings.json`:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-azure-openai-key",
    "DeploymentName": "gpt-4"
  }
}
```

Then update `Program.cs` to use `AddAzureOpenAIChatCompletion` instead of `AddOpenAIChatCompletion`.

## Troubleshooting

### "No OpenAI API key found"
- Make sure you've set the `OPENAI_API_KEY` environment variable
- Or add it to `src/ALAN.Agent/appsettings.json`

### Port Already in Use
- Change the port in `docker-compose.yml` (for Docker)
- Or use `dotnet run --urls "http://localhost:PORT"` (for local .NET)

### SignalR Connection Issues
- The app automatically falls back to polling mode
- Check browser console for connection errors
- Ensure both agent and web are running

## Next Steps

1. **Deploy to Azure**: See [README.md](README.md#deploying-to-azure)
2. **Add Custom Skills**: Extend the agent with Semantic Kernel plugins
3. **Customize UI**: Modify `src/ALAN.Web/Pages/Index.cshtml`
4. **Add Authentication**: Integrate Azure AD or other auth providers

## Support

For issues and questions:
- Check the [README.md](README.md) for detailed documentation
- Review [Semantic Kernel docs](https://learn.microsoft.com/semantic-kernel/)
- Open an issue on GitHub

## Example: Testing the Agent

Once running, you should see:
1. The agent status changing (Idle → Thinking → Acting)
2. Thoughts appearing in the middle panel
3. Actions being logged on the right
4. All updating automatically every ~5 seconds

Enjoy building with ALAN! 🤖
