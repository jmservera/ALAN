import { render, screen } from '@testing-library/react';
import AgentState from './AgentState';

describe('AgentState Component', () => {
  it('renders agent status correctly', () => {
    const state = {
      status: 'Running',
      currentGoal: 'Test goal',
      currentPrompt: 'Test prompt'
    };

    render(<AgentState state={state} />);

    expect(screen.getByText('Agent State')).toBeInTheDocument();
    expect(screen.getByText('Running')).toBeInTheDocument();
    expect(screen.getByText('Test goal')).toBeInTheDocument();
    expect(screen.getByText('Test prompt')).toBeInTheDocument();
  });

  it('applies correct status color for running state', () => {
    const state = {
      status: 'Running',
      currentGoal: 'Test goal'
    };

    render(<AgentState state={state} />);
    
    const statusBadge = screen.getByText('Running');
    expect(statusBadge).toHaveStyle({ backgroundColor: '#4caf50' });
  });

  it('applies correct status color for paused state', () => {
    const state = {
      status: 'Paused',
      currentGoal: 'Test goal'
    };

    render(<AgentState state={state} />);
    
    const statusBadge = screen.getByText('Paused');
    expect(statusBadge).toHaveStyle({ backgroundColor: '#ff9800' });
  });

  it('applies correct status color for error state', () => {
    const state = {
      status: 'Error',
      currentGoal: 'Test goal'
    };

    render(<AgentState state={state} />);
    
    const statusBadge = screen.getByText('Error');
    expect(statusBadge).toHaveStyle({ backgroundColor: '#f44336' });
  });

  it('does not render prompt section when prompt is not provided', () => {
    const state = {
      status: 'Running',
      currentGoal: 'Test goal'
    };

    render(<AgentState state={state} />);

    expect(screen.queryByText('Current Prompt:')).not.toBeInTheDocument();
  });

  it('handles empty or unknown status gracefully', () => {
    const state = {
      status: 'Unknown',
      currentGoal: 'Test goal'
    };

    render(<AgentState state={state} />);
    
    const statusBadge = screen.getByText('Unknown');
    expect(statusBadge).toHaveStyle({ backgroundColor: '#888' });
  });
});
