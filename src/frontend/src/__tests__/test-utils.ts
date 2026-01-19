import { ReactElement } from 'react';
import { render, RenderOptions } from '@testing-library/react';

/**
 * Custom render function for tests
 * Can be extended to include providers, wrappers, etc.
 */
export function renderWithProviders(ui: ReactElement, options?: RenderOptions) {
  return render(ui, { ...options });
}

/**
 * Create a test file with specified content
 */
export function createTestFile(name: string, content: string = 'test content', type: string = 'text/plain') {
  return new File([content], name, { type });
}

/**
 * Format bytes to human-readable size
 */
export function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 Bytes';
  const k = 1024;
  const sizes = ['Bytes', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
}

/**
 * Mock fetch for API testing
 */
export function mockFetch(response: any, options: { status?: number; ok?: boolean } = {}) {
  const { status = 200, ok = status < 400 } = options;
  global.fetch = vi.fn(() =>
    Promise.resolve({
      ok,
      status,
      json: () => Promise.resolve(response),
      text: () => Promise.resolve(JSON.stringify(response)),
    })
  ) as any;
}
