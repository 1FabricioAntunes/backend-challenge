import type React from 'react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { fetchFileStatus, type FileStatusResponse } from '../api/fileClient';
import FileDetailsPanel from './FileDetailsPanel';
import StatusBadge, { type FileStatus } from './StatusBadge';
import '../styles/FileStatusComponent.css';

export type FileRecord = {
  id: string;
  name: string;
  uploadTime: string;
  status: FileStatus;
  transactionCount?: number;
  errorMessage?: string;
};

const POLLING_DELAYS_MS = [3000, 6000, 10000];

const seedFiles: FileRecord[] = [
  {
    id: 'file-004',
    name: 'cnab_jan_processed.txt',
    uploadTime: '2026-01-17T14:25:00Z',
    status: 'Processed',
    transactionCount: 124,
  },
  {
    id: 'file-005',
    name: 'cnab_pending.txt',
    uploadTime: '2026-01-18T08:10:00Z',
    status: 'Processing',
    transactionCount: 86,
  },
  {
    id: 'file-006',
    name: 'cnab_rejected.txt',
    uploadTime: '2026-01-18T06:45:00Z',
    status: 'Rejected',
    transactionCount: 0,
    errorMessage: 'Linha 23 inválida: campo de valor ausente.',
  },
  {
    id: 'file-007',
    name: 'cnab_uploaded.txt',
    uploadTime: '2026-01-19T02:05:00Z',
    status: 'Uploaded',
  },
];
const formatDateTime = (value: string) => {
  const date = new Date(value);
  const pad = (n: number) => n.toString().padStart(2, '0');
  const day = pad(date.getDate());
  const month = pad(date.getMonth() + 1);
  const year = date.getFullYear();
  const hours = pad(date.getHours());
  const minutes = pad(date.getMinutes());
  const seconds = pad(date.getSeconds());
  return `${day}/${month}/${year} ${hours}:${minutes}:${seconds}`;
};

export default function FileStatusComponent() {
  const [files, setFiles] = useState<FileRecord[]>(seedFiles);
  const [selectedFileId, setSelectedFileId] = useState<string | null>(seedFiles[0]?.id ?? null);
  const [isPolling, setIsPolling] = useState<boolean>(false);
  const [pollingDelayMs, setPollingDelayMs] = useState<number>(POLLING_DELAYS_MS[0]);
  const [backoffStep, setBackoffStep] = useState<number>(0);
  const [isRefreshing, setIsRefreshing] = useState<boolean>(false);
  const [lastCheckedTime, setLastCheckedTime] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const sortedFiles = useMemo(() => {
    return [...files].sort(
      (a, b) => new Date(b.uploadTime).getTime() - new Date(a.uploadTime).getTime()
    );
  }, [files]);

  const selectedFile = useMemo(
    () => sortedFiles.find((file) => file.id === selectedFileId) ?? null,
    [sortedFiles, selectedFileId]
  );

  const isActiveStatus = (status: FileStatus) => status === 'Processing' || status === 'Uploaded';

  const refreshStatus = useCallback(async () => {
    setIsRefreshing(true);
    const activeFiles = files.filter((file) => isActiveStatus(file.status));

    if (activeFiles.length === 0) {
      setIsPolling(false);
      setIsRefreshing(false);
      return;
    }

    console.info(
      `[file-status] polling ${activeFiles.length} active file(s): ${activeFiles
        .map((file) => file.id)
        .join(', ')}`
    );

    try {
      const responses = await Promise.all(
        activeFiles.map((file) => fetchFileStatus(file.id))
      );

      setFiles((previous) => {
        const responseMap = new Map<string, FileStatusResponse>();
        responses.forEach((response) => responseMap.set(response.id, response));

        let hasActive = false;

        const updated = previous.map((file) => {
          if (!responseMap.has(file.id)) return file;
          const update = responseMap.get(file.id)!;
          const next = { ...file, ...update };
          if (isActiveStatus(next.status)) {
            hasActive = true;
          }
          return next;
        });

        if (!hasActive) {
          setIsPolling(false);
        }

        return updated;
      });

      setLastCheckedTime(new Date().toISOString());
      setError(null);

      if (backoffStep !== 0) {
        setBackoffStep(0);
        setPollingDelayMs(POLLING_DELAYS_MS[0]);
      }
    } catch (err) {
      console.error('[file-status] polling failed', err);
      setError('Unable to update status. Will try again shortly.');

      setBackoffStep((currentStep) => {
        const nextStep = Math.min(currentStep + 1, POLLING_DELAYS_MS.length - 1);
        setPollingDelayMs(POLLING_DELAYS_MS[nextStep]);
        return nextStep;
      });
    }
    setIsRefreshing(false);
  }, [backoffStep, files]);

  useEffect(() => {
    const hasActive = files.some((file) => isActiveStatus(file.status));
    if (hasActive) {
      setIsPolling(true);
    } else {
      setIsPolling(false);
    }
  }, [files]);

  useEffect(() => {
    if (!isPolling) return;

    const hasActive = files.some((file) => isActiveStatus(file.status));
    if (!hasActive) {
      setIsPolling(false);
      return;
    }

    const timeoutId = window.setTimeout(() => {
      refreshStatus();
    }, pollingDelayMs);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [files, isPolling, pollingDelayMs, refreshStatus]);

  const handleSelect = (fileId: string) => {
    setSelectedFileId(fileId);
  };

  const handleManualRefresh = () => {
    setError(null);
    setBackoffStep(0);
    setPollingDelayMs(POLLING_DELAYS_MS[0]);
    setIsPolling(true);
    refreshStatus();
  };

  const handleRetryFile = (fileId: string) => {
    setFiles((previous) =>
      previous.map((file) =>
        file.id === fileId
          ? { ...file, status: 'Uploaded', errorMessage: undefined }
          : file
      )
    );
    setError(null);
    setBackoffStep(0);
    setPollingDelayMs(POLLING_DELAYS_MS[0]);
    setIsPolling(true);
  };

  const handleKeyNavigation = (
    event: React.KeyboardEvent<HTMLButtonElement>,
    currentIndex: number
  ) => {
    if (sortedFiles.length === 0) return;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      const nextIndex = (currentIndex + 1) % sortedFiles.length;
      setSelectedFileId(sortedFiles[nextIndex].id);
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault();
      const nextIndex = (currentIndex - 1 + sortedFiles.length) % sortedFiles.length;
      setSelectedFileId(sortedFiles[nextIndex].id);
    }
  };

  const handleDownloadReport = (fileId: string) => {
    console.info(`Download report requested for ${fileId}`);
  };

  const handleCloseDetail = () => {
    setSelectedFileId(null);
  };

  const formatTime = (value: string | null) => {
    if (!value) return '—';
    return new Date(value).toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    });
  };

  return (
    <section className="file-status">
      <header className="file-status__header">
        <div>
          <p className="file-status__eyebrow">Monitoring</p>
          <h2 className="file-status__title">Processing Status</h2>
          <p className="file-status__subtitle">
            Track uploaded files, processing states and failure details.
          </p>
        </div>
        <div className="file-status__actions">
          <button className="file-status__button" onClick={handleManualRefresh} aria-label="Refresh status">
            Refresh list
          </button>
          <span className={`polling-indicator ${isPolling ? 'polling-indicator--on' : ''}`}>
            {isRefreshing && <span className="spinner" role="status" aria-label="Refreshing" />}
            {isPolling ? 'Polling active' : 'Polling inactive'} · Last update: {formatTime(lastCheckedTime)}
          </span>
        </div>
      </header>

      {error && (
        <div className="file-status__error" role="alert">
          <p>{error}</p>
          <div className="file-status__error-actions">
            <button
              className="file-status__button file-status__button--secondary"
              onClick={() => setError(null)}
            >
              Dismiss
            </button>
            <button className="file-status__button" onClick={handleManualRefresh}>
              Try again
            </button>
          </div>
        </div>
      )}

      <div className="file-status__layout">
        <div className="file-status__table-card">
          <div className="table__meta">
            <p className="table__meta-count">{sortedFiles.length} files</p>
            <p className="table__meta-note">Sorted by most recent</p>
          </div>

          <div className="table__wrapper" role="table" aria-label="File status">
            <div className="table__row table__row--head" role="row">
              <div className="table__cell table__cell--head" role="columnheader">
                File
              </div>
              <div className="table__cell table__cell--head" role="columnheader">
                Uploaded at
              </div>
              <div className="table__cell table__cell--head" role="columnheader">
                Status
              </div>
              <div className="table__cell table__cell--head table__cell--right" role="columnheader">
                Transactions
              </div>
            </div>

            {sortedFiles.length === 0 && (
              <div className="empty-state" role="status">
                <p>No files found.</p>
                <button className="file-status__button" onClick={handleManualRefresh}>
                  Refresh
                </button>
              </div>
            )}

            {sortedFiles.map((file, index) => (
              <button
                key={file.id}
                type="button"
                className={`table__row table__row--body ${
                  selectedFileId === file.id ? 'table__row--active' : ''
                }`}
                onClick={() => handleSelect(file.id)}
                role="row"
                onKeyDown={(event) => handleKeyNavigation(event, index)}
                aria-pressed={selectedFileId === file.id}
              >
                <div className="table__cell" role="cell">
                  <p className="file-name">{file.name}</p>
                  <p className="file-id">ID: {file.id}</p>
                </div>
                <div className="table__cell" role="cell">
                  <p className="file-date">{formatDateTime(file.uploadTime)}</p>
                </div>
                <div className="table__cell" role="cell">
                  <StatusBadge status={file.status} />
                </div>
                <div className="table__cell table__cell--right" role="cell">
                  {typeof file.transactionCount === 'number' ? file.transactionCount : '—'}
                </div>
              </button>
            ))}
          </div>
        </div>

        <aside className="file-status__detail" aria-live="polite">
          {selectedFile ? (
            <FileDetailsPanel
              file={selectedFile}
              lastCheckedTime={lastCheckedTime}
              onRetry={() => handleRetryFile(selectedFile.id)}
              onDownloadReport={() => handleDownloadReport(selectedFile.id)}
              onClose={handleCloseDetail}
            />
          ) : (
            <div className="detail-placeholder">
              <p>Select a file to view details.</p>
            </div>
          )}
        </aside>
      </div>
    </section>
  );
}
