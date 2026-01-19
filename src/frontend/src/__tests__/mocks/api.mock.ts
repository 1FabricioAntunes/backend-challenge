import { vi } from 'vitest';

/**
 * Mock apiClient for consistent test behavior
 */
export const createMockApiClient = () => ({
  post: vi.fn(),
  get: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
});

/**
 * Mock axios error response
 */
export const createAxiosError = (status: number, data: any = {}) => ({
  isAxiosError: true,
  response: {
    status,
    data,
  },
});

/**
 * Mock successful API response
 */
export const createAxiosResponse = <T>(data: T) => ({
  data,
  status: 200,
  statusText: 'OK',
});
