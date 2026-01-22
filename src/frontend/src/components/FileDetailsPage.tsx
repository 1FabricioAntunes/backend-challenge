import { useEffect, useState } from 'react';
import { fileApi } from '../services/api';
import StatusBadge, { type FileStatus } from './StatusBadge';
import '../styles/FileDetailsPage.css';

type FileDto = {
  id: string;
  fileName: string;
  status: string; // Will be converted to FileStatus
  uploadedAt: string;
  processedAt?: string | null;
  transactionCount?: number | null;
  errorMessage?: string | null;
};

type FileDetailsPageProps = {
  fileId: string;
  onBack: () => void;
};

export default function FileDetailsPage({ fileId, onBack }: FileDetailsPageProps) {
  const [file, setFile] = useState<FileDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const loadFile = async () => {
      try {
        setLoading(true);
        setError(null);
        const fileData = await fileApi.getFile(fileId);
        setFile(fileData);
      } catch (err: any) {
        console.error('Failed to load file details:', err);
        setError(err.response?.data?.message || 'Failed to load file details. Please try again.');
      } finally {
        setLoading(false);
      }
    };

    loadFile();
  }, [fileId]);

  const formatDateTime = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    });
  };

  const formatDuration = (start: string, end: string | null | undefined) => {
    const startTime = new Date(start).getTime();
    const endTime = end ? new Date(end).getTime() : Date.now();
    const deltaMs = Math.max(0, endTime - startTime);

    const totalSeconds = Math.floor(deltaMs / 1000);
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;

    if (hours > 0) {
      return `${hours}h ${minutes}m ${seconds}s`;
    } else if (minutes > 0) {
      return `${minutes}m ${seconds}s`;
    } else {
      return `${seconds}s`;
    }
  };

  if (loading) {
    return (
      <div className="file-details">
        <div className="file-details__loading">Loading file details...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="file-details">
        <div className="file-details__error" role="alert">
          <p>{error}</p>
          <button onClick={onBack}>Back to Files</button>
        </div>
      </div>
    );
  }

  if (!file) {
    return (
      <div className="file-details">
        <div className="file-details__error" role="alert">
          <p>File not found</p>
          <button onClick={onBack}>Back to Files</button>
        </div>
      </div>
    );
  }

  return (
    <div className="file-details">
      <header className="file-details__header">
        <button className="file-details__back-button" onClick={onBack} aria-label="Back to file list">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M19 12H5M12 19l-7-7 7-7" />
          </svg>
          Back to Files
        </button>
        <h2 className="file-details__title">File Processing Details</h2>
      </header>

      <div className="file-details__content">
        <div className="file-details__card">
          <div className="file-details__section">
            <h3 className="file-details__section-title">File Information</h3>
            <div className="file-details__info-grid">
              <div className="file-details__info-item">
                <span className="file-details__info-label">File Name</span>
                <span className="file-details__info-value">{file.fileName}</span>
              </div>
              <div className="file-details__info-item">
                <span className="file-details__info-label">File ID</span>
                <span className="file-details__info-value file-details__info-value--mono">{file.id}</span>
              </div>
              <div className="file-details__info-item">
                <span className="file-details__info-label">Status</span>
                <span className="file-details__info-value">
                  <StatusBadge status={file.status as FileStatus} />
                </span>
              </div>
            </div>
          </div>

          <div className="file-details__section">
            <h3 className="file-details__section-title">Timing Information</h3>
            <div className="file-details__info-grid">
              <div className="file-details__info-item">
                <span className="file-details__info-label">Uploaded At</span>
                <span className="file-details__info-value">{formatDateTime(file.uploadedAt)}</span>
              </div>
              {file.processedAt && (
                <div className="file-details__info-item">
                  <span className="file-details__info-label">Processed At</span>
                  <span className="file-details__info-value">{formatDateTime(file.processedAt)}</span>
                </div>
              )}
              <div className="file-details__info-item">
                <span className="file-details__info-label">Processing Duration</span>
                <span className="file-details__info-value">
                  {file.processedAt
                    ? formatDuration(file.uploadedAt, file.processedAt)
                    : formatDuration(file.uploadedAt, null)}
                </span>
              </div>
            </div>
          </div>

          <div className="file-details__section">
            <h3 className="file-details__section-title">Processing Results</h3>
            <div className="file-details__info-grid">
              <div className="file-details__info-item">
                <span className="file-details__info-label">Transaction Count</span>
                <span className="file-details__info-value">
                  {file.transactionCount !== null && file.transactionCount !== undefined
                    ? file.transactionCount
                    : (file.status as FileStatus) === 'Processed'
                    ? '0'
                    : 'Processing...'}
                </span>
              </div>
            </div>
          </div>

          {file.errorMessage && (
            <div className="file-details__section">
              <h3 className="file-details__section-title">Error Information</h3>
              <div className="file-details__error-box" role="alert">
                <p className="file-details__error-message">{file.errorMessage}</p>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
