# ALAN.Web - React Frontend

A modern React application for observing and interacting with the ALAN autonomous agent.

## Prerequisites

- Node.js 18+ and npm
- ALAN.ChatApi running (provides the backend API)

## Setup

1. Install dependencies:
```bash
npm install
```

2. Create environment file:
```bash
cp .env.example .env
```

3. Update `.env` with your API URL if different from default:
```
VITE_API_URL=http://localhost:5001
```

## Development

Start the development server:
```bash
npm run dev
```

The app will be available at http://localhost:5269

## Features

- **Real-time Agent Observability**: View agent status, current goals, and prompts
- **Thoughts & Actions**: See the agent's recent thoughts and actions in real-time
- **Structured Reasoning Display**: Automatically parses and beautifully displays structured JSON thoughts with reasoning and planned actions
- **Human Steering**: Send guidance, pause/resume the agent
- **CopilotKit Integration**: Built-in AI assistant that can answer questions about the agent's state

### Structured Thought Format

The UI automatically detects and beautifully renders thoughts in this JSON format:

```json
{
  "reasoning": "Building on my previous success...",
  "actions": [
    {
      "action": "Search Microsoft Learn for courses",
      "goal": "Find structured training",
      "extra": "This extends my knowledge from previous iterations"
    }
  ]
}
```

Features:
- 🎬 Action badges showing the number of planned actions
- 💭 Highlighted reasoning sections
- 🎯 Goal and context display for each action
- Numbered action items for easy reference

## Build

Build for production:
```bash
npm run build
```

The built files will be in the `dist/` directory.

## Architecture

This is a pure React application built with:
- **React 18** with TypeScript
- **Vite** for fast development and building
- **CopilotKit** for AI assistant integration
- **React Router** for navigation

All server-side logic has been moved to ALAN.ChatApi. This app communicates with the backend via:
- REST API for state polling and commands
- WebSocket (via CopilotKit) for AI chat functionality
