import axios, { AxiosInstance, AxiosError, InternalAxiosRequestConfig } from 'axios';
import { ApiError } from '../types';

// Local storage keys (must match AuthContext)
const TOKEN_KEY = 'auth_token';
const USER_KEY = 'auth_user';

/**
 * SECURITY NOTE: Token Storage in localStorage
 * 
 * ⚠️ XSS Vulnerability: Tokens stored in localStorage are accessible to JavaScript,
 * making them vulnerable to Cross-Site Scripting (XSS) attacks.
 * 
 * Current Implementation (Development/Demo):
 * - Tokens retrieved from localStorage and injected into Authorization header
 * - Suitable for development and demo purposes
 * 
 * Production Recommendation (per docs/security.md):
 * - Use HttpOnly cookies for token storage (prevents JavaScript access)
 * - Browser automatically sends cookies with requests
 * - No need to manually inject Authorization header
 * - Backend must set cookies with HttpOnly, Secure, and SameSite flags
 * 
 * See: docs/security.md - "Insecure Token Storage" section
 * See: technical-decisions.md - "JWT Security Considerations" section
 */

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
  baseURL: import.meta.env.VITE_API_URL || import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000',
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
    const response = await apiClient.get<{ items: any[]; totalCount: number; page: number; pageSize: number }>('/api/files/v1', {
      params: { page, pageSize },
    });
    // Map backend response format to frontend expected format
    return {
      files: response.data.items || [],
      total: response.data.totalCount || 0,
    };
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
    pageSize = 50,
    storeId?: string,
    startDate?: string,
    endDate?: string
  ): Promise<{ items: any[]; totalCount: number; page: number; pageSize: number }> => {
    const params: Record<string, string | number> = { page, pageSize };
    if (storeId) params.storeId = storeId;
    if (startDate) params.startDate = startDate;
    if (endDate) params.endDate = endDate;
    
    const response = await apiClient.get<{ items: any[]; totalCount: number; page: number; pageSize: number }>(
      '/api/transactions/v1',
      { params }
    );
    // Handle both response formats: direct data or nested in data property
    const data = response.data as any;
    // If response has items property, use it; otherwise assume it's the PagedResult directly
    if (data.items) {
      return data;
    }
    // Fallback: if response structure is different, try to extract items
    return {
      items: Array.isArray(data) ? data : (data.items || []),
      totalCount: data.totalCount || data.total || 0,
      page: data.page || page,
      pageSize: data.pageSize || pageSize,
    };
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
  getStores: async (): Promise<any[]> => {
    const response = await apiClient.get<any[]>('/api/stores/v1');
    // API returns array directly
    return Array.isArray(response.data) ? response.data : [];
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
 * Authentication API endpoints
 * 
 * Note: For development/demo, this uses a simple login endpoint.
 * In production with OAuth2/Cognito, the flow would redirect to Cognito
 * for authentication and receive tokens via OAuth2 callback.
 */
export const authApi = {
  /**
   * Login with email and password
   * POST /api/auth/v1/login
   * 
   * @param email - User email address
   * @param password - User password
   * @returns Promise with user info and JWT token
   */
  login: async (email: string, password: string): Promise<{ user: { name: string; email: string }; token: string }> => {
    // Use a separate axios instance without auth interceptor for login
    const loginClient = axios.create({
      baseURL: import.meta.env.VITE_API_URL || import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000',
      headers: {
        'Content-Type': 'application/json',
      },
      timeout: 30000,
    });

    // Add correlation ID to login request
    loginClient.interceptors.request.use((config) => {
      config.headers['X-Correlation-ID'] = generateCorrelationId();
      return config;
    });

    // Add response interceptor to extract API error messages
    loginClient.interceptors.response.use(
      (response) => response,
      (error: AxiosError) => {
        // Extract error message from API response (matches backend ErrorResponse format)
        // Backend returns: { error: { message: "...", code: "...", statusCode: 401 } }
        const responseData = error.response?.data as any;
        
        // Check for structured error response from API
        if (responseData && typeof responseData === 'object') {
          if (responseData.error?.message) {
            // API returned structured error - use the message from API
            const apiError = new Error(responseData.error.message);
            (apiError as any).code = responseData.error.code || 'API_ERROR';
            (apiError as any).statusCode = responseData.error.statusCode || error.response?.status;
            return Promise.reject(apiError);
          }
          
          // Check if message is at root level (alternative format)
          if (responseData.message) {
            const apiError = new Error(responseData.message);
            (apiError as any).code = responseData.code || 'API_ERROR';
            (apiError as any).statusCode = error.response?.status;
            return Promise.reject(apiError);
          }
        }
        
        // Fallback: For 401 errors, provide user-friendly message
        if (error.response?.status === 401) {
          const apiError = new Error('Invalid email or password.');
          (apiError as any).code = 'AUTHENTICATION_FAILED';
          (apiError as any).statusCode = 401;
          return Promise.reject(apiError);
        }
        
        // For other errors, provide generic message (never show raw Axios error)
        const errorMessage = 'An error occurred during login. Please try again.';
        const apiError = new Error(errorMessage);
        (apiError as any).code = 'NETWORK_ERROR';
        (apiError as any).statusCode = error.response?.status;
        return Promise.reject(apiError);
      }
    );

    const response = await loginClient.post<{ user: { name: string; email: string }; token: string }>(
      '/api/auth/v1/login',
      { email, password }
    );
    return response.data;
  },

  /**
   * Logout (client-side only, clears token)
   * In production, might call backend to invalidate token
   */
  logout: async (): Promise<void> => {
    // Client-side logout handled by AuthContext
    // In production, might call: POST /api/auth/v1/logout
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
