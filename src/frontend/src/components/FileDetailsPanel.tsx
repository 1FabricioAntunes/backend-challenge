import StatusBadge, { type FileStatus } from './StatusBadge';
import type { FileRecord } from './FileStatusComponent';

type FileDetailsPanelProps = {
  file: FileRecord;
  lastCheckedTime: string | null;
  onRetry?: () => void;
  onDownloadReport?: () => void;
  onClose?: () => void;
};

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

const formatDuration = (startIso: string, endIso: string | null) => {
  const start = new Date(startIso).getTime();
  const end = endIso ? new Date(endIso).getTime() : Date.now();
  const deltaMs = Math.max(0, end - start);

  const totalSeconds = Math.floor(deltaMs / 1000);
  const hours = Math.floor(totalSeconds / 3600)
    .toString()
    .padStart(2, '0');
  const minutes = Math.floor((totalSeconds % 3600) / 60)
    .toString()
    .padStart(2, '0');
  const seconds = Math.floor(totalSeconds % 60)
    .toString()
    .padStart(2, '0');

  return `${hours}:${minutes}:${seconds}`;
};

const getStatusNote = (status: FileStatus) => {
  if (status === 'Processing') return 'Processing';
  if (status === 'Uploaded') return 'Processing queue';
  if (status === 'Processed') return 'Processed successfully';
  return 'Rejected';
};

export default function FileDetailsPanel({
  file,
  lastCheckedTime,
  onRetry,
  onDownloadReport,
  onClose,
}: FileDetailsPanelProps) {
  const processedCount =
    typeof file.transactionCount === 'number' ? file.transactionCount : undefined;

  const metrics = [
    {
      label: 'Duração',
      value: formatDuration(file.uploadTime, lastCheckedTime),
      helper: lastCheckedTime ? 'Atualizado' : 'Em tempo real',
    },
    {
      label: 'Records',
      value: processedCount !== undefined ? processedCount : 'Processing',
      helper: 'Total transactions',
    },
    {
      label: 'Status',
      value: <StatusBadge status={file.status} />, // badge is self-descriptive
      helper: getStatusNote(file.status),
    },
  ];

  const handleDownload = () => {
    onDownloadReport?.();
  };

  const handleRetry = () => {
    onRetry?.();
  };

  return (
    <div className="detail-card" role="region" aria-live="polite" tabIndex={-1}>
      <div className="detail-card__header">
        <div>
          <p className="detail-eyebrow">Selected file</p>
          <h3 className="detail-title">{file.name}</h3>
          <p className="detail-id">ID: {file.id}</p>
        </div>
        <button
          type="button"
          className="detail-close"
          aria-label="Fechar painel de detalhes"
          onClick={onClose}
        >
          ×
        </button>
      </div>

      <div className="detail-grid">
        <div className="detail-item">
          <p className="detail-label">Uploaded at</p>
          <p className="detail-value">{formatDateTime(file.uploadTime)}</p>
        </div>
        <div className="detail-item">
          <p className="detail-label">Status</p>
          <p className="detail-value">
            <StatusBadge status={file.status} />
          </p>
        </div>
        <div className="detail-item">
          <p className="detail-label">Transactions</p>
          <p className="detail-value">
            {processedCount !== undefined ? processedCount : 'Processing'}
          </p>
        </div>
      </div>

      <div className="detail-metrics">
        {metrics.map((metric) => (
          <div className="metric-card" key={metric.label}>
            <p className="metric-label">{metric.label}</p>
            <p className="metric-value">{metric.value}</p>
            <p className="metric-helper">{metric.helper}</p>
          </div>
        ))}
      </div>

      {file.errorMessage && (
        <div className="detail-error" role="alert">
          <p className="detail-error__title">Rejection reason</p>
          <p className="detail-error__message">{file.errorMessage}</p>
          <p className="detail-error__context">Context line: not available</p>
        </div>
      )}

      <div className="detail-actions">
        <button className="file-status__button" onClick={handleRetry}>
          Retry Upload
        </button>
        <button className="file-status__button file-status__button--secondary" onClick={handleDownload}>
          Download Error Report
        </button>
      </div>
    </div>
  );
}
