import { render, screen, fireEvent } from '@testing-library/react';
import ThoughtsList from './ThoughtsList';

// Mock CopilotKit hooks
jest.mock('@copilotkit/react-core', () => ({
  useCopilotReadable: jest.fn(),
}));

describe('ThoughtsList Component', () => {
  const mockThoughts = [
    {
      id: '1',
      type: 'analysis',
      content: 'Analyzing the problem',
      timestamp: '2025-12-14T10:00:00Z',
      toolCalls: [
        {
          toolName: 'test-tool',
          mcpServer: 'test-server',
          arguments: '{"key": "value"}',
          result: 'Success',
          success: true,
          durationMs: 100
        }
      ]
    },
    {
      id: '2',
      type: 'decision',
      content: 'Making a decision',
      timestamp: '2025-12-14T10:01:00Z'
    }
  ];

  it('renders thoughts list with correct title', () => {
    render(<ThoughtsList thoughts={mockThoughts} />);
    expect(screen.getByText('Recent Thoughts')).toBeInTheDocument();
  });

  it('displays all thoughts', () => {
    render(<ThoughtsList thoughts={mockThoughts} />);
    expect(screen.getByText('Analyzing the problem')).toBeInTheDocument();
    expect(screen.getByText('Making a decision')).toBeInTheDocument();
  });

  it('shows correct icon for analysis type', () => {
    render(<ThoughtsList thoughts={mockThoughts} />);
    const analysisIcon = screen.getByText('🔍');
    expect(analysisIcon).toBeInTheDocument();
  });

  it('shows correct icon for decision type', () => {
    render(<ThoughtsList thoughts={mockThoughts} />);
    const decisionIcon = screen.getByText('🎯');
    expect(decisionIcon).toBeInTheDocument();
  });

  it('opens detail modal when thought is clicked', () => {
    render(<ThoughtsList thoughts={mockThoughts} />);
    
    const thoughtItem = screen.getByText('Analyzing the problem').closest('.thought-item');
    if (thoughtItem) {
      fireEvent.click(thoughtItem);
      // Modal shows the thought type as title
      expect(screen.getAllByText('analysis').length).toBeGreaterThan(1);
    }
  });

  it('displays tool calls when present', () => {
    render(<ThoughtsList thoughts={mockThoughts} />);
    
    const thoughtItem = screen.getByText('Analyzing the problem').closest('.thought-item');
    if (thoughtItem) {
      fireEvent.click(thoughtItem);
      expect(screen.getByText('test-tool')).toBeInTheDocument();
      expect(screen.getByText(/test-server/)).toBeInTheDocument();
    }
  });

  it('formats timestamp correctly', () => {
    render(<ThoughtsList thoughts={mockThoughts} />);
    // Check that timestamps are formatted as time strings (timezone-agnostic)
    const expectedTime1 = new Date('2025-12-14T10:00:00Z').toLocaleTimeString();
    const expectedTime2 = new Date('2025-12-14T10:01:00Z').toLocaleTimeString();
    expect(screen.getByText(expectedTime1)).toBeInTheDocument();
    expect(screen.getByText(expectedTime2)).toBeInTheDocument();
  });

  it('handles empty thoughts list', () => {
    render(<ThoughtsList thoughts={[]} />);
    expect(screen.getByText('Recent Thoughts')).toBeInTheDocument();
    expect(screen.getByText('No thoughts yet...')).toBeInTheDocument();
  });

  it('closes modal when close button is clicked', () => {
    render(<ThoughtsList thoughts={mockThoughts} />);
    
    // Open modal
    const thoughtItem = screen.getByText('Analyzing the problem').closest('.thought-item');
    if (thoughtItem) {
      fireEvent.click(thoughtItem);
      const closeButton = screen.getByText('×');
      expect(closeButton).toBeInTheDocument();
      
      // Close modal
      fireEvent.click(closeButton);
      expect(screen.queryByText('×')).not.toBeInTheDocument();
    }
  });
});
