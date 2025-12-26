'use client';

import './AgentState.css';

interface AgentStateProps {
  state: {
    status: string;
    currentGoal: string;
    currentPrompt?: string;
  };
}

function AgentState({ state }: AgentStateProps) {
  const getStatusColor = (status: string) => {
    switch (status.toLowerCase()) {
      case 'running':
      case 'active':
        return '#4caf50';
      case 'paused':
        return '#ff9800';
      case 'error':
        return '#f44336';
      default:
        return '#888';
    }
  };

  return (
    <div className="panel agent-state">
      <h2>Agent State</h2>
      
      <div className="state-item">
        <span className="state-label">Status:</span>
        <span 
          className="state-value status-badge"
          style={{ backgroundColor: getStatusColor(state.status) }}
        >
          {state.status}
        </span>
      </div>

      <div className="state-item">
        <span className="state-label">Current Goal:</span>
        <span className="state-value">{state.currentGoal}</span>
      </div>

      {state.currentPrompt && (
        <div className="state-item">
          <span className="state-label">Current Prompt:</span>
          <pre className="state-value prompt-text">{state.currentPrompt}</pre>
        </div>
      )}
    </div>
  );
}

export default AgentState;
