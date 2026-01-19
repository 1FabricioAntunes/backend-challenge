# Frontend Testing Guide

## Overview

Frontend tests are organized following React Testing Library best practices with clear separation of concerns.

## Directory Structure

```text
src/
├── __tests__/                          # All test files (centralized)
│   ├── setup.ts                        # Vitest configuration & globals
│   ├── test-utils.ts                   # Testing utilities & helpers
│   ├── mocks/
│   │   ├── api.mock.ts                 # API client mocks
│   │   └── handlers.mock.ts            # Request handlers (MSW)
│   ├── fixtures/
│   │   └── transactions.fixture.ts     # Test data & fixtures
│   ├── unit/
│   │   ├── utils/
│   │   │   └── fileValidation.test.ts
│   │   └── services/
│   │       └── api.test.ts
│   ├── components/
│   │   ├── FileUploadComponent.test.tsx
│   │   ├── TransactionQueryComponent.test.tsx
│   │   └── StoreBalanceComponent.test.tsx
│   └── integration/
│       ├── upload-workflow.test.tsx
│       └── query-workflow.test.tsx
├── components/
│   ├── FileUploadComponent.tsx
│   └── ...
├── services/
│   └── api.ts
└── utils/
    └── fileValidation.ts
```

## Running Tests

```bash
# Run all tests
npm test

# Run tests in watch mode
npm test -- --watch

# Run with UI dashboard
npm run test:ui

# Generate coverage report
npm run test:coverage

# Run specific test file
npm test -- FileUploadComponent.test.tsx

# Run tests matching pattern
npm test -- --grep "Upload"
```

## Testing Patterns

### 1. Unit Tests (Utilities & Services)

**Location**: `__tests__/unit/`

Test pure functions and services in isolation.

```typescript
import { describe, it, expect } from 'vitest';
import { validateCnabFile } from '@/utils/fileValidation';

describe('fileValidation', () => {
  it('should reject files > 10MB', async () => {
    const largeFile = new File(['x'.repeat(11 * 1024 * 1024)], 'large.txt');
    const result = await validateCnabFile(largeFile);
    expect(result.isValid).toBe(false);
    expect(result.error).toContain('too large');
  });
});
```

### 2. Component Tests

**Location**: `__tests__/components/`

Test React components using React Testing Library.

**Best practices**:

- Query by user-visible labels (role, text, label)
- Test user interactions, not implementation
- Use `screen` queries instead of destructuring
- Mock external dependencies (API, child components)

```typescript
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import FileUploadComponent from '@/components/FileUploadComponent';

describe('FileUploadComponent', () => {
  it('should upload file on submit', async () => {
    render(<FileUploadComponent />);
    
    const input = screen.getByLabelText('Select file');
    fireEvent.change(input, { target: { files: [file] } });
    
    const button = screen.getByRole('button', { name: /upload/i });
    fireEvent.click(button);
    
    await waitFor(() => {
      expect(screen.getByText(/success/i)).toBeInTheDocument();
    });
  });
});
```

### 3. Integration Tests

**Location**: `__tests__/integration/`

Test complete workflows across multiple components.

```typescript
describe('Upload Workflow', () => {
  it('should upload file and show status', async () => {
    // Test full flow: select → upload → see status
  });
});
```

## Mocking

### Mock API Client

```typescript
import { createMockApiClient } from '@/__tests__/mocks/api.mock';

vi.mock('@/services/api', () => ({
  default: createMockApiClient(),
}));
```

### Mock Axios Errors

```typescript
import { createAxiosError } from '@/__tests__/mocks/api.mock';

apiClient.post.mockRejectedValue(
  createAxiosError(400, { message: 'Bad request' })
);
```

### Use Fixtures

```typescript
import { mockTransactions, mockStores } from '@/__tests__/fixtures/transactions.fixture';

it('should display transactions', () => {
  apiClient.get.mockResolvedValue({ data: mockTransactions });
});
```

## Assertions

### Common Patterns

```typescript
// Elements
expect(screen.getByRole('button')).toBeInTheDocument();
expect(screen.getByText(/upload/i)).toBeVisible();

// Attributes
expect(button).toBeDisabled();
expect(input).toHaveValue('text');

// Async
await waitFor(() => {
  expect(screen.getByText(/success/)).toBeInTheDocument();
});
```

## Setup & Teardown

- **Global setup**: `__tests__/setup.ts` runs before all tests
- **Component cleanup**: Automatic after each test via RTL
- **Mock cleanup**: `vi.clearAllMocks()` in beforeEach

## Coverage Goals

| Category | Target |
|----------|--------|
| Statements | 80% |
| Branches | 75% |
| Functions | 80% |
| Lines | 80% |

Run `npm run test:coverage` to check coverage.

## Debugging Tests

### Print DOM

```typescript
import { screen, debug } from '@testing-library/react';

debug(); // Print current DOM
debug(screen.getByRole('button')); // Print specific element
```

### Use test.only

```typescript
it.only('debug this test', () => {
  // Only this test runs
});
```

### Run with UI

```bash
npm run test:ui
```

## Common Issues

### "Not wrapped in act(...)"

Wrap state updates in `waitFor` or `act`:

```typescript
await waitFor(() => {
  expect(state).toBe(expected);
});
```

### "Cannot find module"

Ensure imports use correct paths:

- Relative: `../components/Button`
- Alias: `@/components/Button`

### "Timeout" Errors

Increase timeout or check mock setup:

```typescript
await waitFor(() => {...}, { timeout: 5000 });
```

## Resources

- [React Testing Library Docs](https://testing-library.com/docs/react-testing-library/intro/)
- [Vitest Docs](https://vitest.dev/)
- [Testing Best Practices](https://kentcdodds.com/blog/common-mistakes-with-react-testing-library)
