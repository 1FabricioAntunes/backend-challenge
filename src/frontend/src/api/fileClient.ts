import apiClient from '../services/api';
import type { FileStatus } from '../components/StatusBadge';

export type FileStatusResponse = {
  id: string;
  name: string;
  uploadTime: string;
  status: FileStatus;
  transactionCount?: number;
  errorMessage?: string;
};

export const fetchFileStatus = async (fileId: string): Promise<FileStatusResponse> => {
  const response = await apiClient.get<FileStatusResponse>(`/api/files/v1/${fileId}/status`);
  return response.data;
};
