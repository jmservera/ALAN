import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import HumanInputPanel from './HumanInputPanel';

// Mock fetch
global.fetch = jest.fn();

describe('HumanInputPanel Component', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (global.fetch as jest.Mock).mockResolvedValue({
      ok: true,
      json: async () => ({}),
    });
  });

  it('renders human input panel', () => {
    render(<HumanInputPanel />);
    expect(screen.getByText('Human Steering')).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/provide guidance/i)).toBeInTheDocument();
  });

  it('updates input value when typing', async () => {
    render(<HumanInputPanel />);
    
    const input = screen.getByPlaceholderText(/provide guidance/i) as HTMLTextAreaElement;
    await userEvent.type(input, 'Test input');
    
    expect(input.value).toBe('Test input');
  });

  it('calls fetch API with input value when form is submitted', async () => {
    render(<HumanInputPanel />);
    
    const input = screen.getByPlaceholderText(/provide guidance/i);
    const submitButton = screen.getByRole('button', { name: /send input/i });
    
    await userEvent.type(input, 'Test guidance');
    fireEvent.click(submitButton);
    
    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith('/api/input', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          type: 'UserInput',
          content: 'Test guidance',
        }),
      });
    });
  });

  it('clears input after successful submission', async () => {
    render(<HumanInputPanel />);
    
    const input = screen.getByPlaceholderText(/provide guidance/i) as HTMLTextAreaElement;
    const submitButton = screen.getByRole('button', { name: /send input/i });
    
    await userEvent.type(input, 'Test guidance');
    fireEvent.click(submitButton);
    
    await waitFor(() => {
      expect(input.value).toBe('');
    });
  });

  it('shows success message after submission', async () => {
    render(<HumanInputPanel />);
    
    const input = screen.getByPlaceholderText(/provide guidance/i);
    const submitButton = screen.getByRole('button', { name: /send input/i });
    
    await userEvent.type(input, 'Test');
    fireEvent.click(submitButton);
    
    await waitFor(() => {
      expect(screen.getByText('Input sent successfully!')).toBeInTheDocument();
    });
  });

  it('does not submit empty input', async () => {
    render(<HumanInputPanel />);
    
    const submitButton = screen.getByRole('button', { name: /send input/i });
    fireEvent.click(submitButton);
    
    expect(global.fetch).not.toHaveBeenCalled();
  });

  it('handles fetch errors gracefully', async () => {
    (global.fetch as jest.Mock).mockRejectedValueOnce(new Error('Network error'));
    
    render(<HumanInputPanel />);
    
    const input = screen.getByPlaceholderText(/provide guidance/i);
    const submitButton = screen.getByRole('button', { name: /send input/i });
    
    await userEvent.type(input, 'Test');
    fireEvent.click(submitButton);
    
    await waitFor(() => {
      expect(screen.getByText('Network error')).toBeInTheDocument();
    });
  });

  it('renders pause button', () => {
    render(<HumanInputPanel />);
    expect(screen.getByRole('button', { name: /pause agent/i })).toBeInTheDocument();
  });

  it('renders resume button', () => {
    render(<HumanInputPanel />);
    expect(screen.getByRole('button', { name: /resume agent/i })).toBeInTheDocument();
  });

  it('renders advanced controls', () => {
    render(<HumanInputPanel />);
    expect(screen.getByText('Advanced Controls')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /trigger batch learning/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /consolidate memory/i })).toBeInTheDocument();
  });
});
