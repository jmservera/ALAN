# Testing Setup for ALAN.Web

## Overview

The ALAN.Web Next.js application uses **Jest** and **React Testing Library** for component and integration testing.

## Testing Stack

- **Jest 29** - Test runner and assertion library
- **React Testing Library 16** - Component testing utilities
- **@testing-library/jest-dom** - Custom Jest matchers for DOM assertions
- **@testing-library/user-event** - User interaction simulation
- **jest-environment-jsdom** - Browser-like environment for tests

## Running Tests

```bash
# Install dependencies first
npm install

# Run all tests once
npm test

# Run tests in watch mode (useful during development)
npm run test:watch

# Run tests with coverage report
npm run test:coverage
```

## Test Structure

Tests are located alongside their components with the `.test.tsx` extension:

```
src/
  components/
    AgentState.tsx
    AgentState.test.tsx      # Tests for AgentState component
    ThoughtsList.tsx
    ThoughtsList.test.tsx    # Tests for ThoughtsList component
    HumanInputPanel.tsx
    HumanInputPanel.test.tsx # Tests for HumanInputPanel component
```

## Writing Tests

### Basic Component Test

```typescript
import { render, screen } from '@testing-library/react';
import MyComponent from './MyComponent';

describe('MyComponent', () => {
  it('renders correctly', () => {
    render(<MyComponent title="Test" />);
    expect(screen.getByText('Test')).toBeInTheDocument();
  });
});
```

### Testing User Interactions

```typescript
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import MyForm from './MyForm';

describe('MyForm', () => {
  it('handles form submission', async () => {
    const handleSubmit = jest.fn();
    render(<MyForm onSubmit={handleSubmit} />);
    
    const input = screen.getByRole('textbox');
    await userEvent.type(input, 'Hello');
    
    const button = screen.getByRole('button', { name: /submit/i });
    await userEvent.click(button);
    
    expect(handleSubmit).toHaveBeenCalledWith('Hello');
  });
});
```

### Mocking CopilotKit Hooks

Since ALAN.Web uses CopilotKit, you'll need to mock these hooks in tests:

```typescript
jest.mock('@copilotkit/react-core', () => ({
  useCopilotReadable: jest.fn(),
  useCopilotAction: jest.fn(),
}));
```

## Test Configuration

### jest.config.ts

- Configures Jest with Next.js integration
- Sets up module path aliases (`@/*` maps to `src/*`)
- Defines coverage collection settings
- Specifies test file patterns

### jest.setup.ts

- Imports `@testing-library/jest-dom` for enhanced matchers
- Sets up environment variables for tests
- Can be extended with global test utilities or mocks

## Best Practices

1. **Follow AAA Pattern** - Arrange, Act, Assert structure
   ```typescript
   it('does something', () => {
     // Arrange - Set up test data and render
     const props = { value: 'test' };
     render(<Component {...props} />);
     
     // Act - Perform action
     fireEvent.click(screen.getByRole('button'));
     
     // Assert - Verify outcome
     expect(screen.getByText('result')).toBeInTheDocument();
   });
   ```

2. **Test User Behavior, Not Implementation**
   - Query by role, label, or text that users see
   - Avoid querying by class names or test IDs unless necessary
   - Use `screen.getByRole()` when possible

3. **Use Semantic Queries**
   - Prefer: `screen.getByRole('button', { name: /submit/i })`
   - Over: `screen.getByTestId('submit-button')`

4. **Test Accessibility**
   - Use `getByRole()` to ensure proper ARIA roles
   - Verify keyboard navigation works
   - Check for proper labels and alternative text

5. **Keep Tests Isolated**
   - Each test should be independent
   - Use `beforeEach()` to reset state
   - Mock external dependencies

6. **Test Edge Cases**
   - Empty states
   - Error conditions
   - Loading states
   - Boundary values

## Coverage Goals

Target coverage levels:
- **Statements**: 80%+
- **Branches**: 75%+
- **Functions**: 80%+
- **Lines**: 80%+

View coverage report after running:
```bash
npm run test:coverage
# Open coverage/lcov-report/index.html in browser
```

## Integration with CI/CD

Tests should be run in CI pipeline before deployment. Add to your GitHub Actions workflow:

```yaml
- name: Install dependencies
  run: npm ci
  working-directory: ./src/ALAN.Web

- name: Run tests
  run: npm test
  working-directory: ./src/ALAN.Web

- name: Upload coverage
  uses: codecov/codecov-action@v3
  with:
    files: ./src/ALAN.Web/coverage/lcov.info
```

## Common Testing Patterns

### Testing Async Operations

```typescript
import { waitFor } from '@testing-library/react';

it('loads data', async () => {
  render(<DataComponent />);
  
  await waitFor(() => {
    expect(screen.getByText('Loaded')).toBeInTheDocument();
  });
});
```

### Testing State Changes

```typescript
it('toggles visibility', async () => {
  render(<ToggleComponent />);
  
  const button = screen.getByRole('button');
  expect(screen.queryByText('Hidden')).not.toBeInTheDocument();
  
  await userEvent.click(button);
  expect(screen.getByText('Hidden')).toBeInTheDocument();
});
```

### Mocking API Calls

```typescript
global.fetch = jest.fn(() =>
  Promise.resolve({
    json: () => Promise.resolve({ data: 'test' }),
  })
) as jest.Mock;
```

## Resources

- [React Testing Library Documentation](https://testing-library.com/docs/react-testing-library/intro/)
- [Jest Documentation](https://jestjs.io/docs/getting-started)
- [Next.js Testing Documentation](https://nextjs.org/docs/testing)
- [Common Testing Mistakes](https://kentcdodds.com/blog/common-mistakes-with-react-testing-library)
