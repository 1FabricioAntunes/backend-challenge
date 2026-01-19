import axios, { AxiosInstance, AxiosError, InternalAxiosRequestConfig } from 'axios';
import { ApiError } from '../types';

// Local storage keys (must match AuthContext)
const TOKEN_KEY = 'auth_token';
const USER_KEY = 'auth_user';

/**
 * Generate a unique correlation ID for request tracking
 * Format: timestamp-random
 */
const generateCorrelationId = (): string => {
  return `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
};

/**
 * Placeholder for token refresh logic (future extension)
 * 
 * @returns Promise<string | null> - New access token or null if refresh fails
 * 
 * TODO: Implement refresh token logic when backend supports it
 * - Call refresh token endpoint
 * - Update localStorage with new token
 * - Return new token for retry
 */
const refreshToken = async (): Promise<string | null> => {
  // Stub implementation - to be implemented when backend supports refresh tokens
  console.warn('Token refresh not yet implemented');
  return null;
};

/**
 * Clear authentication state and redirect to login
 */
const clearAuthAndRedirect = (): void => {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
  
  // Redirect to login page if not already there
  if (window.location.pathname !== '/login') {
    window.location.href = '/login';
  }
};

/**
 * Create Axios instance with base configuration
 */
const apiClient: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000',
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 30000,
});

/**
 * Request interceptor: Add correlation ID and Authorization header to all requests
 */
apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    // Add correlation ID for request tracking
    config.headers['X-Correlation-ID'] = generateCorrelationId();
    
    // Inject Authorization Bearer token from localStorage
    const token = localStorage.getItem(TOKEN_KEY);
    if (token) {
      config.headers['Authorization'] = `Bearer ${token}`;
    }
    
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

/**
 * Response interceptor: Handle errors, 401 authentication failures, and normalize error responses
 */
apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiError>) => {
    // Handle 401 Unauthorized - clear auth and redirect
    if (error.response?.status === 401) {
      console.warn('Authentication failed (401). Clearing session and redirecting to login.');
      
      // Optional: Attempt token refresh before redirecting (future enhancement)
      // const newToken = await refreshToken();
      // if (newToken && error.config) {
      //   error.config.headers['Authorization'] = `Bearer ${newToken}`;
      //   return apiClient.request(error.config);
      // }
      
      clearAuthAndRedirect();
      
      return Promise.reject({
        code: 'UNAUTHORIZED',
        message: 'Authentication required. Please login again.',
      } as ApiError);
    }

    // If the server returned an error response, use it
    if (error.response?.data) {
      return Promise.reject(error.response.data);
    }

    // For network or other errors, create a standard error object
    return Promise.reject({
      code: 'NETWORK_ERROR',
      message: error.message || 'An unknown error occurred',
    } as ApiError);
  }
);

/**
 * File API endpoints
 */
export const fileApi = {
  /**
   * Upload a CNAB file
   * POST /api/files/v1
   */
  uploadFile: async (file: File): Promise<{ fileId: string }> => {
    const formData = new FormData();
    formData.append('file', file);

    const response = await apiClient.post<{ fileId: string }>(
      '/api/files/v1',
      formData,
      {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      }
    );
    return response.data;
  },

  /**
   * Get list of uploaded files with pagination
   * GET /api/files/v1?page=1&pageSize=10
   */
  getFiles: async (page = 1, pageSize = 10): Promise<{ files: any[]; total: number }> => {
    const response = await apiClient.get<{ files: any[]; total: number }>('/api/files/v1', {
      params: { page, pageSize },
    });
    return response.data;
  },

  /**
   * Get file details and status
   * GET /api/files/v1/{fileId}
   */
  getFile: async (fileId: string): Promise<any> => {
    const response = await apiClient.get(`/api/files/v1/${fileId}`);
    return response.data;
  },

  /**
   * Get transactions for a specific file
   * GET /api/files/v1/{fileId}/transactions
   */
  getFileTransactions: async (fileId: string): Promise<{ transactions: any[] }> => {
    const response = await apiClient.get<{ transactions: any[] }>(
      `/api/files/v1/${fileId}/transactions`
    );
    return response.data;
  },
};

/**
 * Transaction API endpoints
 */
export const transactionApi = {
  /**
   * Get transactions with optional filtering
   * GET /api/transactions/v1
   */
  getTransactions: async (
    page = 1,
    pageSize = 50
  ): Promise<{ transactions: any[]; total: number }> => {
    const response = await apiClient.get<{ transactions: any[]; total: number }>(
      '/api/transactions/v1',
      {
        params: { page, pageSize },
      }
    );
    return response.data;
  },

  /**
   * Get transaction details
   * GET /api/transactions/v1/{transactionId}
   */
  getTransaction: async (transactionId: string): Promise<any> => {
    const response = await apiClient.get(`/api/transactions/v1/${transactionId}`);
    return response.data;
  },
};

/**
 * Store API endpoints
 */
export const storeApi = {
  /**
   * Get list of stores
   * GET /api/stores/v1
   */
  getStores: async (): Promise<{ stores: any[] }> => {
    const response = await apiClient.get<{ stores: any[] }>('/api/stores/v1');
    return response.data;
  },

  /**
   * Get store details
   * GET /api/stores/v1/{storeCode}
   */
  getStore: async (storeCode: string): Promise<any> => {
    const response = await apiClient.get(`/api/stores/v1/${storeCode}`);
    return response.data;
  },
};

/**
 * Re-export configured axios instance for direct use
 * 
 * Features:
 * - Automatic Authorization header injection
 * - Correlation ID tracking
 * - 401 handling with redirect to login
 * - Standardized error responses
 * 
 * @example
 * ```tsx
 * import apiClient from './services/api';
 * 
 * const response = await apiClient.get('/api/endpoint');
 * ```
 */
export default apiClient;

/**
 * Export helper for token refresh (future extension)
 */
export { refreshToken };
