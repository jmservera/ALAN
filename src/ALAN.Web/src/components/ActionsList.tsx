'use client';

import { useState } from 'react';
import { useCopilotReadable } from '@copilotkit/react-core';
import './ActionsList.css';

interface ToolCall {
  toolName: string;
  mcpServer?: string;
  arguments?: string;
  result?: string;
  success: boolean;
  durationMs?: number;
}

interface Action {
  id: string;
  name: string;
  status: string;
  description?: string;
  input?: string;
  output?: string;
  toolCalls?: ToolCall[];
  timestamp: string;
}

interface ActionsListProps {
  actions: Action[];
}

function ActionsList({ actions }: ActionsListProps) {
  const [selectedAction, setSelectedAction] = useState<Action | null>(null);
  
  // Make actions readable by CopilotKit
  useCopilotReadable({
    description: 'Recent actions taken by the autonomous agent',
    value: actions,
  });

  const getStatusColor = (status: string) => {
    switch (status.toLowerCase()) {
      case 'completed':
      case 'success':
        return '#4caf50';
      case 'running':
      case 'inprogress':
        return '#2196f3';
      case 'failed':
      case 'error':
        return '#f44336';
      default:
        return '#888';
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status.toLowerCase()) {
      case 'completed':
      case 'success':
        return '✓';
      case 'running':
      case 'inprogress':
        return '⟳';
      case 'failed':
      case 'error':
        return '✗';
      default:
        return '○';
    }
  };

  return (
    <div className="panel actions-list">
      <h2>Recent Actions</h2>
      {actions.length === 0 ? (
        <p className="empty-message">No actions yet...</p>
      ) : (
        <div className="actions-container">
          {actions.slice().reverse().map((action) => (
            <div 
              key={action.id} 
              className="action-item"
              onClick={() => setSelectedAction(action)}
              style={{ cursor: 'pointer' }}
            >
              <div className="action-header">
                <span 
                  className="action-status-icon"
                  style={{ color: getStatusColor(action.status) }}
                >
                  {getStatusIcon(action.status)}
                </span>
                <span className="action-name">{action.name}</span>
                {action.toolCalls && action.toolCalls.length > 0 && (
                  <span className="tool-badge" title={`${action.toolCalls.length} tool calls`}>
                    🔧 {action.toolCalls.length}
                  </span>
                )}
                <span className="action-time">
                  {new Date(action.timestamp).toLocaleTimeString()}
                </span>
              </div>
              <div 
                className="action-status"
                style={{ color: getStatusColor(action.status) }}
              >
                {action.status}
              </div>
              {action.output && (
                <div className="action-result">
                  <strong>Output:</strong> {action.output.substring(0, 100)}{action.output.length > 100 ? '...' : ''}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
      
      {selectedAction && (
        <div className="modal-overlay" onClick={() => setSelectedAction(null)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>{selectedAction.name}</h3>
              <button className="modal-close" onClick={() => setSelectedAction(null)}>×</button>
            </div>
            <div className="modal-body">
              <div className="detail-section">
                <strong>Status:</strong>
                <span style={{ color: getStatusColor(selectedAction.status), marginLeft: '8px' }}>
                  {getStatusIcon(selectedAction.status)} {selectedAction.status}
                </span>
              </div>
              
              <div className="detail-section">
                <strong>Timestamp:</strong>
                <span style={{ marginLeft: '8px' }}>
                  {new Date(selectedAction.timestamp).toLocaleString()}
                </span>
              </div>
              
              {selectedAction.description && (
                <div className="detail-section">
                  <strong>Description:</strong>
                  <p>{selectedAction.description}</p>
                </div>
              )}
              
              {selectedAction.input && (
                <div className="detail-section">
                  <strong>Input:</strong>
                  <pre>{selectedAction.input}</pre>
                </div>
              )}
              
              {selectedAction.output && (
                <div className="detail-section">
                  <strong>Output:</strong>
                  <pre>{selectedAction.output}</pre>
                </div>
              )}
              
              {selectedAction.toolCalls && selectedAction.toolCalls.length > 0 && (
                <div className="detail-section">
                  <strong>MCP Tool Calls ({selectedAction.toolCalls.length}):</strong>
                  <div className="tool-calls-list">
                    {selectedAction.toolCalls.map((tool, idx) => (
                      <div key={idx} className="tool-call-item">
                        <div className="tool-call-header">
                          <span className={`tool-status ${tool.success ? 'success' : 'error'}`}>
                            {tool.success ? '✓' : '✗'}
                          </span>
                          <strong>{tool.toolName}</strong>
                          {tool.mcpServer && (
                            <span className="mcp-server">({tool.mcpServer})</span>
                          )}
                          {tool.durationMs && (
                            <span className="tool-duration">{tool.durationMs.toFixed(0)}ms</span>
                          )}
                        </div>
                        {tool.arguments && (
                          <div className="tool-detail">
                            <strong>Arguments:</strong>
                            <pre>{tool.arguments}</pre>
                          </div>
                        )}
                        {tool.result && (
                          <div className="tool-detail">
                            <strong>Result:</strong>
                            <pre>{tool.result}</pre>
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default ActionsList;
