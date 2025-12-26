'use client';

import { useEffect, useState } from 'react';
import { useCopilotReadable } from '@copilotkit/react-core';
import AgentState from './AgentState';
import ThoughtsList from './ThoughtsList';
import ActionsList from './ActionsList';
import HumanInputPanel from './HumanInputPanel';
import './Dashboard.css';

interface AgentStateData {
  status: string;
  currentGoal: string;
  currentPrompt?: string;
  recentThoughts: AgentThought[];
  recentActions: AgentAction[];
}

interface AgentThought {
  id: string;
  type: string;
  content: string;
  timestamp: string;
}

interface AgentAction {
  id: string;
  name: string;
  status: string;
  result?: string;
  error?: string;
  timestamp: string;
}

function Dashboard() {
  const [state, setState] = useState<AgentStateData | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Get the API URL from environment or use default

  // Make the agent state readable by CopilotKit
  useCopilotReadable({
    description: 'Current state of the ALAN autonomous agent',
    value: state,
  });

  useEffect(() => {
    // Poll for state updates
    const pollInterval = setInterval(async () => {
      try {
        const response = await fetch(`/api/state`);
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }
        const data = await response.json();
        setState(data);
        setError(null);
      } catch (err) {
        console.error('Error fetching agent state:', err);
        setError(err instanceof Error ? err.message : 'Unknown error');
      }
    }, 1000); // Poll every second

    return () => clearInterval(pollInterval);
  }, []);

  if (error) {
    return (
      <div className="dashboard error">
        <h1>ALAN Dashboard</h1>
        <div className="error-message">
          <h2>Connection Error</h2>
          <p>{error}</p>
          <p>Make sure ALAN.ChatApi is running</p>
        </div>
      </div>
    );
  }

  if (!state) {
    return (
      <div className="dashboard loading">
        <h1>ALAN Dashboard</h1>
        <p>Loading agent state...</p>
      </div>
    );
  }

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <h1>ALAN - Autonomous Learning Agent Network</h1>
        <p className="subtitle">Real-time Agent Observability</p>
      </header>

      <div className="dashboard-grid">
        <div className="left-panel">
          <AgentState state={state} />
          <HumanInputPanel />
        </div>

        <div className="center-panel">
          <ThoughtsList thoughts={state.recentThoughts || []} />
        </div>

        <div className="right-panel">
          <ActionsList actions={state.recentActions || []} />
        </div>
      </div>
    </div>
  );
}

export default Dashboard;
