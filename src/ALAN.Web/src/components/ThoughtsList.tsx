'use client';

import { useState } from 'react';
import { useCopilotReadable } from '@copilotkit/react-core';
import './ThoughtsList.css';

interface ToolCall {
  toolName: string;
  mcpServer?: string;
  arguments?: string;
  result?: string;
  success: boolean;
  durationMs?: number;
}

interface Thought {
  id: string;
  type: string;
  content: string;
  timestamp: string;
  toolCalls?: ToolCall[];
}

interface StructuredAction {
  action: string;
  goal?: string;
  extra?: string;
}

interface StructuredContent {
  reasoning?: string;
  actions?: StructuredAction[];
}

interface ThoughtsListProps {
  thoughts: Thought[];
}

function ThoughtsList({ thoughts }: ThoughtsListProps) {
  const [selectedThought, setSelectedThought] = useState<Thought | null>(null);
  
  // Make thoughts readable by CopilotKit
  useCopilotReadable({
    description: 'Recent thoughts from the autonomous agent',
    value: thoughts,
  });

  const parseStructuredContent = (content: string): { structured: StructuredContent | null; text: string } => {
    try {
      const parsed = JSON.parse(content);
      if (parsed && (parsed.reasoning || parsed.actions)) {
        return { structured: parsed, text: content };
      }
    } catch {
      // Not JSON, return as plain text
    }
    return { structured: null, text: content };
  };

  const getThoughtIcon = (type: string) => {
    switch (type.toLowerCase()) {
      case 'analysis':
        return '🔍';
      case 'decision':
        return '🎯';
      case 'planning':
        return '📋';
      case 'reflection':
        return '💭';
      default:
        return '💡';
    }
  };

  return (
    <div className="panel thoughts-list">
      <h2>Recent Thoughts</h2>
      {thoughts.length === 0 ? (
        <p className="empty-message">No thoughts yet...</p>
      ) : (
        <div className="thoughts-container">
          {thoughts.slice().reverse().map((thought) => {
            const { structured, text } = parseStructuredContent(thought.content);
            const displayText = structured?.reasoning 
              ? structured.reasoning.substring(0, 150) 
              : text.substring(0, 150);
            
            return (
              <div 
                key={thought.id} 
                className="thought-item"
                onClick={() => setSelectedThought(thought)}
                style={{ cursor: 'pointer' }}
              >
                <div className="thought-header">
                  <span className="thought-icon">{getThoughtIcon(thought.type)}</span>
                  <span className="thought-type">{thought.type}</span>
                  {structured?.actions && structured.actions.length > 0 && (
                    <span className="action-badge" title={`${structured.actions.length} planned actions`}>
                      🎬 {structured.actions.length}
                    </span>
                  )}
                  {thought.toolCalls && thought.toolCalls.length > 0 && (
                    <span className="tool-badge" title={`${thought.toolCalls.length} tool calls`}>
                      🔧 {thought.toolCalls.length}
                    </span>
                  )}
                  <span className="thought-time">
                    {new Date(thought.timestamp).toLocaleTimeString()}
                  </span>
                </div>
                <div className="thought-content">
                  {displayText}{(structured?.reasoning || text).length > 150 ? '...' : ''}
                </div>
              </div>
            );
          })}
        </div>
      )}
      
      {selectedThought && (
        <div className="modal-overlay" onClick={() => setSelectedThought(null)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>
                <span className="thought-icon">{getThoughtIcon(selectedThought.type)}</span>
                {selectedThought.type}
              </h3>
              <button className="modal-close" onClick={() => setSelectedThought(null)}>×</button>
            </div>
            <div className="modal-body">
              <div className="detail-section">
                <strong>Timestamp:</strong>
                <span style={{ marginLeft: '8px' }}>
                  {new Date(selectedThought.timestamp).toLocaleString()}
                </span>
              </div>
              
              {(() => {
                const { structured, text } = parseStructuredContent(selectedThought.content);
                
                if (structured) {
                  return (
                    <>
                      {structured.reasoning && (
                        <div className="detail-section">
                          <strong>💭 Reasoning:</strong>
                          <p className="reasoning-text">{structured.reasoning}</p>
                        </div>
                      )}
                      
                      {structured.actions && structured.actions.length > 0 && (
                        <div className="detail-section">
                          <strong>🎬 Planned Actions ({structured.actions.length}):</strong>
                          <div className="actions-list">
                            {structured.actions.map((action, idx) => (
                              <div key={idx} className="action-item">
                                <div className="action-header">
                                  <span className="action-number">{idx + 1}</span>
                                  <strong>{action.action}</strong>
                                </div>
                                {action.goal && (
                                  <div className="action-detail">
                                    <span className="action-label">🎯 Goal:</span>
                                    <p>{action.goal}</p>
                                  </div>
                                )}
                                {action.extra && (
                                  <div className="action-detail">
                                    <span className="action-label">ℹ️ Context:</span>
                                    <p>{action.extra}</p>
                                  </div>
                                )}
                              </div>
                            ))}
                          </div>
                        </div>
                      )}
                    </>
                  );
                } else {
                  return (
                    <div className="detail-section">
                      <strong>Content:</strong>
                      <p>{text}</p>
                    </div>
                  );
                }
              })()}
              
              {selectedThought.toolCalls && selectedThought.toolCalls.length > 0 && (
                <div className="detail-section">
                  <strong>MCP Tool Calls ({selectedThought.toolCalls.length}):</strong>
                  <div className="tool-calls-list">
                    {selectedThought.toolCalls.map((tool, idx) => (
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

export default ThoughtsList;
