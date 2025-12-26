# ALAN.Web Migration to React

## Overview

ALAN.Web has been converted from an ASP.NET Core Razor Pages application to a pure React application. All server-side logic has been moved to ALAN.ChatApi.

## Changes

### ALAN.Web (Now React App)

**What Changed:**
- Converted from ASP.NET Core + Razor Pages to React + TypeScript + Vite
- Removed all C# code, controllers, and SignalR hubs
- Created modern React components for UI
- Uses Vite for fast development and building
- Removed from .NET solution file

**Technology Stack:**
- React 18 with TypeScript
- Vite for build tooling
- CopilotKit for AI assistant integration
- React Router for navigation

**Development:**
```bash
cd src/ALAN.Web
npm install
npm run dev
```

**Build:**
```bash
npm run build
```

**Port:** Still runs on port 5269 (via Vite dev server)

### ALAN.ChatApi (New Backend)

**What Changed:**
- Now hosts all server-side logic previously in ALAN.Web
- Added AgentStateService (polls Azure Storage for state updates)
- Added REST API controllers:
  - `StateController` - Get agent state
  - `HumanInputController` - Send inputs, pause/resume agent
  - `CodeProposalController` - Code proposal management
- Enhanced CORS configuration for React app
- Added JSON enum serialization

**API Endpoints:**
- `GET /api/state` - Get current agent state
- `POST /api/input` - Submit human input
- `POST /api/prompt` - Update agent prompt
- `POST /api/pause` - Pause agent
- `POST /api/resume` - Resume agent
- `GET /api/proposals` - Get code proposals
- `POST /api/proposals/{id}/approve` - Approve proposal
- `POST /api/proposals/{id}/reject` - Reject proposal
- `/copilotkit` - CopilotKit/AG-UI endpoint (WebSocket)

**Port:** Runs on port 5001

## Running the System

### Development Mode

**Terminal 1 - ALAN.Agent:**
```bash
cd src/ALAN.Agent
dotnet run
```

**Terminal 2 - ALAN.ChatApi:**
```bash
cd src/ALAN.ChatApi
dotnet run
```

**Terminal 3 - ALAN.Web (React):**
```bash
cd src/ALAN.Web
npm install  # First time only
npm run dev
```

**Access:** http://localhost:5269

### Production Build

```bash
# Build backend
dotnet build ALAN.sln

# Build frontend
cd src/ALAN.Web
npm install
npm run build
```

## VS Code Debug Configuration

Use the compound launch configuration:
- **"Launch Agent + ChatApi + Web"** - Starts all three components
  - C#: Launch ALAN.Agent
  - C#: Launch ALAN.ChatApi
  - Launch ALAN.Web (React)

## Migration Impact

### Removed

- `ALAN.Web` C# project removed from solution
- `ALAN.Web.Tests` deleted (no longer applicable)
- SignalR hub in ALAN.Web (logic moved to ALAN.ChatApi)
- Razor Pages and C# controllers in ALAN.Web

### Added

- Modern React application in ALAN.Web
- npm build tasks in `.vscode/tasks.json`
- AgentStateService in ALAN.ChatApi
- REST API controllers in ALAN.ChatApi
- CopilotKit UI integration

### Configuration Changes

**ALAN.Web `.env`:**
```env
VITE_API_URL=http://localhost:5001
```

**ALAN.ChatApi (existing):**
- Uses existing environment variables
- Added CORS for `http://localhost:5269`

## Architecture Benefits

1. **Separation of Concerns:** Clear separation between frontend (React) and backend (ASP.NET Core)
2. **Modern Frontend:** Leverages latest React ecosystem and tooling
3. **API-First:** ALAN.ChatApi now provides a complete REST API
4. **Easier Development:** Independent frontend/backend development and deployment
5. **Better Performance:** Vite provides fast HMR and optimized production builds

## Testing

### Backend Tests
```bash
dotnet test
```

### Frontend (Manual)
1. Start all services (Agent, ChatApi, Web)
2. Open http://localhost:5269
3. Verify:
   - Agent state displays correctly
   - Thoughts and actions appear in real-time
   - Human input controls work (send input, pause, resume)
   - CopilotKit sidebar functions

## Deployment Considerations

### ALAN.Web (React)
- Build output in `dist/` directory
- Serve as static files via nginx, CDN, or static hosting
- Environment variables via `.env` files or build-time injection

### ALAN.ChatApi
- Deploy as ASP.NET Core application
- Ensure CORS is configured for production frontend URL
- Update `AllowedOrigins` configuration

## Troubleshooting

**"Connection Error" in frontend:**
- Ensure ALAN.ChatApi is running on port 5001
- Check `VITE_API_URL` in `.env` file
- Verify CORS is enabled in ChatApi

**CopilotKit not connecting:**
- Ensure `/copilotkit` endpoint is accessible
- Check WebSocket connection in browser dev tools
- Verify Azure OpenAI configuration

**Build errors:**
- Run `npm install` in ALAN.Web
- Ensure Node.js 18+ is installed
- Clear `node_modules` and reinstall if needed
