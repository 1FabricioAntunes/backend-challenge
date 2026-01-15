import axios, { AxiosInstance, AxiosError } from 'axios';
import { ApiError } from '../types';

/**
 * Generate a unique correlation ID for request tracking
 * Format: timestamp-random
 */
const generateCorrelationId = (): string => {
  return `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
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
 * Request interceptor: Add correlation ID header to all requests
 */
apiClient.interceptors.request.use(
  (config) => {
    config.headers['X-Correlation-ID'] = generateCorrelationId();
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

/**
 * Response interceptor: Handle errors and normalize error responses
 */
apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ApiError>) => {
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

export default apiClient;
