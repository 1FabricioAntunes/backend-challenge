import { useEffect, useState, useRef, useCallback } from 'react';
import { fileApi } from '../services/api';
import StatusBadge, { type FileStatus } from './StatusBadge';
import '../styles/FileListComponent.css';
import '../styles/FileStatusComponent.css';

type FileDto = {
  id: string;
  fileName: string;
  status: string; // Will be converted to FileStatus
  uploadedAt: string;
  processedAt?: string | null;
  transactionCount?: number | null;
  errorMessage?: string | null;
};

type FileListComponentProps = {
  onFileSelect: (fileId: string) => void;
};

// Polling interval in milliseconds (3 seconds)
const POLLING_INTERVAL_MS = 3000;

export default function FileListComponent({ onFileSelect }: FileListComponentProps) {
  const [files, setFiles] = useState<FileDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const pollingIntervalRef = useRef<number | null>(null);
  const pageSize = 20;

  /**
   * Check if any file requires polling (Uploaded or Processing status)
   */
  const shouldPoll = useCallback((filesList: FileDto[]): boolean => {
    return filesList.some(file => file.status === 'Uploaded' || file.status === 'Processing');
  }, []);

  const loadFiles = useCallback(async () => {
    try {
      setError(null);
      const response = await fileApi.getFiles(page, pageSize);
      const filesData = response.files || [];
      setFiles(filesData);
      setTotal(response.total || 0);
      
      // Stop polling if all files are in terminal states
      if (!shouldPoll(filesData)) {
        if (pollingIntervalRef.current !== null) {
          clearInterval(pollingIntervalRef.current);
          pollingIntervalRef.current = null;
        }
      }
    } catch (err: any) {
      console.error('Failed to load files:', err);
      setError(err.response?.data?.message || 'Failed to load files. Please try again.');
      // Stop polling on error
      if (pollingIntervalRef.current !== null) {
        clearInterval(pollingIntervalRef.current);
        pollingIntervalRef.current = null;
      }
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, shouldPoll]);

  /**
   * Initial load and setup polling
   */
  useEffect(() => {
    // Initial load
    loadFiles();

    // Cleanup function
    return () => {
      if (pollingIntervalRef.current !== null) {
        clearInterval(pollingIntervalRef.current);
        pollingIntervalRef.current = null;
      }
    };
  }, [page, loadFiles]);

  /**
   * Setup polling when files require it
   */
  useEffect(() => {
    // Only poll if there are files in non-terminal states
    if (files.length > 0 && shouldPoll(files)) {
      // Clear any existing interval
      if (pollingIntervalRef.current !== null) {
        clearInterval(pollingIntervalRef.current);
      }

      // Set up polling interval
      pollingIntervalRef.current = window.setInterval(() => {
        loadFiles();
      }, POLLING_INTERVAL_MS);
    } else {
      // Stop polling if all files reached terminal states
      if (pollingIntervalRef.current !== null) {
        clearInterval(pollingIntervalRef.current);
        pollingIntervalRef.current = null;
      }
    }

    // Cleanup on unmount or when files change
    return () => {
      if (pollingIntervalRef.current !== null) {
        clearInterval(pollingIntervalRef.current);
        pollingIntervalRef.current = null;
      }
    };
  }, [files, shouldPoll, loadFiles]);

  const formatDateTime = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString('en-US', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    });
  };

  const totalPages = Math.ceil(total / pageSize);

  return (
    <div className="file-list">
      <header className="file-list__header">
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flex: 1 }}>
          <div>
            <h2 className="file-list__title">Uploaded Files</h2>
            <p className="file-list__subtitle">
              View all uploaded files and their processing status
            </p>
          </div>
          {files.length > 0 && shouldPoll(files) && (
            <span 
              style={{ 
                fontSize: '0.875rem', 
                color: '#666',
                display: 'flex',
                alignItems: 'center',
                gap: '6px'
              }}
              aria-label="Auto-refreshing status"
            >
              <span style={{ 
                display: 'inline-block',
                width: '8px',
                height: '8px',
                borderRadius: '50%',
                backgroundColor: '#007bff',
                animation: 'pulse 2s ease-in-out infinite'
              }}></span>
              Auto-refreshing...
            </span>
          )}
        </div>
        <button
          className="file-list__refresh-button"
          onClick={loadFiles}
          disabled={loading}
          aria-label="Refresh file list"
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M21.5 2v6h-6M2.5 22v-6h6M2 11.5a10 10 0 0 1 18.8-4.3M22 12.5a10 10 0 0 1-18.8 4.2" />
          </svg>
          Refresh
        </button>
      </header>

      {error && (
        <div className="file-list__error" role="alert">
          <p>{error}</p>
          <button onClick={loadFiles}>Try Again</button>
        </div>
      )}

      {loading && files.length === 0 ? (
        <div className="file-list__loading">Loading files...</div>
      ) : files.length === 0 ? (
        <div className="file-list__empty">
          <p>No files uploaded yet.</p>
          <p>Upload a CNAB file to get started.</p>
        </div>
      ) : (
        <>
          <div className="file-list__table-wrapper">
            <table className="file-list__table">
              <thead>
                <tr>
                  <th>Date/Time</th>
                  <th>File Name</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {files.map((file) => (
                  <tr key={file.id}>
                    <td className="file-list__date">{formatDateTime(file.uploadedAt)}</td>
                    <td className="file-list__filename">{file.fileName}</td>
                    <td className="file-list__status">
                      <StatusBadge status={file.status as FileStatus} />
                    </td>
                    <td className="file-list__actions">
                      <button
                        className="file-list__view-button"
                        onClick={() => onFileSelect(file.id)}
                        aria-label={`View details for ${file.fileName}`}
                        title="View file details"
                      >
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                          <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                          <circle cx="12" cy="12" r="3" />
                        </svg>
                        View
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="file-list__pagination">
              <button
                className="file-list__page-button"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1 || loading}
              >
                Previous
              </button>
              <span className="file-list__page-info">
                Page {page} of {totalPages} ({total} files)
              </span>
              <button
                className="file-list__page-button"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page === totalPages || loading}
              >
                Next
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
