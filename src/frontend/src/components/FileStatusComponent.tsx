import { useMemo, useState } from 'react';
import StatusBadge, { type FileStatus } from './StatusBadge';
import '../styles/FileStatusComponent.css';

type FileRecord = {
  id: string;
  name: string;
  uploadTime: string;
  status: FileStatus;
  transactionCount?: number;
  errorMessage?: string;
};

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

  const handleSelect = (fileId: string) => {
    setSelectedFileId(fileId);
  };

  const handleManualRefresh = () => {
    setError(null);
    setIsPolling(false);
    setFiles((existing) => [...existing]);
  };

  return (
    <section className="file-status">
      <header className="file-status__header">
        <div>
          <p className="file-status__eyebrow">Monitoramento</p>
          <h2 className="file-status__title">Status de Processamento</h2>
          <p className="file-status__subtitle">
            Acompanhe arquivos enviados, estados de processamento e detalhes de falhas.
          </p>
        </div>
        <div className="file-status__actions">
          <button className="file-status__button" onClick={handleManualRefresh} aria-label="Atualizar status">
            Atualizar lista
          </button>
          <span className={`polling-indicator ${isPolling ? 'polling-indicator--on' : ''}`}>
            {isPolling ? 'Polling ativo' : 'Polling inativo'}
          </span>
        </div>
      </header>

      {error && (
        <div className="file-status__error" role="alert">
          <p>{error}</p>
          <button className="file-status__button file-status__button--secondary" onClick={() => setError(null)}>
            Dispensar
          </button>
        </div>
      )}

      <div className="file-status__layout">
        <div className="file-status__table-card">
          <div className="table__meta">
            <p className="table__meta-count">{sortedFiles.length} arquivos</p>
            <p className="table__meta-note">Ordenados por mais recente</p>
          </div>

          <div className="table__wrapper" role="table" aria-label="Status dos arquivos">
            <div className="table__row table__row--head" role="row">
              <div className="table__cell table__cell--head" role="columnheader">
                Arquivo
              </div>
              <div className="table__cell table__cell--head" role="columnheader">
                Enviado em
              </div>
              <div className="table__cell table__cell--head" role="columnheader">
                Status
              </div>
              <div className="table__cell table__cell--head table__cell--right" role="columnheader">
                Transações
              </div>
            </div>

            {sortedFiles.map((file) => (
              <button
                key={file.id}
                type="button"
                className={`table__row table__row--body ${
                  selectedFileId === file.id ? 'table__row--active' : ''
                }`}
                onClick={() => handleSelect(file.id)}
                role="row"
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
            <div className="detail-card">
              <p className="detail-eyebrow">Arquivo selecionado</p>
              <h3 className="detail-title">{selectedFile.name}</h3>
              <p className="detail-id">ID: {selectedFile.id}</p>

              <div className="detail-grid">
                <div className="detail-item">
                  <p className="detail-label">Status</p>
                  <p className="detail-value">
                    <StatusBadge status={selectedFile.status} />
                  </p>
                </div>
                <div className="detail-item">
                  <p className="detail-label">Enviado em</p>
                  <p className="detail-value">{formatDateTime(selectedFile.uploadTime)}</p>
                </div>
                <div className="detail-item">
                  <p className="detail-label">Transações</p>
                  <p className="detail-value">
                    {typeof selectedFile.transactionCount === 'number'
                      ? selectedFile.transactionCount
                      : 'Em processamento'}
                  </p>
                </div>
              </div>

              {selectedFile.errorMessage && (
                <div className="detail-error" role="alert">
                  <p className="detail-error__title">Motivo da rejeição</p>
                  <p className="detail-error__message">{selectedFile.errorMessage}</p>
                </div>
              )}

              <div className="detail-actions">
                <button className="file-status__button">Retry</button>
                <button className="file-status__button file-status__button--secondary">
                  Download relatório
                </button>
              </div>
            </div>
          ) : (
            <div className="detail-placeholder">
              <p>Selecione um arquivo para ver detalhes.</p>
            </div>
          )}
        </aside>
      </div>
    </section>
  );
}
